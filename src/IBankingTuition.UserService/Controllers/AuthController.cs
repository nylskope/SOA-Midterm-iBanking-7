using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IBankingTuition.UserService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IBankingTuition.UserService.Controllers;

[ApiController]
[Route("api/users")]
public class AuthController : ControllerBase {
    private readonly UserDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(UserDbContext context, IConfiguration config) {
        _context = context;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request) {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.PasswordHash == request.Password);

        if (user == null) return Unauthorized(new { message = "Invalid credentials" });

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_config["Jwt:Secret"]);
        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(new[] { new Claim("id", user.UserId) }),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return Ok(new {
            token = tokenHandler.WriteToken(token),
            user = new { user.UserId, user.FullName, user.Balance }
        });
    }

    [HttpGet("{id}/balance")]
    public async Task<IActionResult> GetBalance(string id) {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        return Ok(new { user.UserId, user.Balance, user.RowVersion });
    }

    [HttpPost("{id}/deduct")]
    public async Task<IActionResult> DeductBalance(string id, [FromBody] DeductRequest req) {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        if (user.Balance < req.Amount)
            return BadRequest("Insufficient funds.");

        if (user.RowVersion != req.RowVersion)
            return Conflict("Concurrency conflict. Balance was updated by another process.");

        user.Balance -= req.Amount;
        user.RowVersion += 1;

        try {
            await _context.SaveChangesAsync();
            return Ok(new { user.UserId, user.Balance, user.RowVersion });
        } catch (DbUpdateConcurrencyException) {
            return Conflict("Concurrency conflict occurred during save.");
        }
    }

    [HttpPost("{id}/refund")]
    public async Task<IActionResult> RefundBalance(string id, [FromBody] RefundRequest req) {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Balance += req.Amount;
        user.RowVersion += 1;
        await _context.SaveChangesAsync();

        return Ok(new { user.UserId, user.Balance, user.RowVersion });
    }
}

public record LoginRequest(string Username, string Password);
public record DeductRequest(decimal Amount, int RowVersion);
public record RefundRequest(decimal Amount);
