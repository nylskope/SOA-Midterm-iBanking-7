using IBankingTuition.Infrastructure.Adapters;
using IBankingTuition.OtpService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IBankingTuition.OtpService.Controllers;

[ApiController]
[Route("api/otps")]
public class OtpController : ControllerBase
{
    private readonly RedisOtpRepository _repository;
    private readonly IEmailAdapter _emailAdapter;

    public OtpController(RedisOtpRepository repository, IEmailAdapter emailAdapter)
    {
        _repository = repository;
        _emailAdapter = emailAdapter;
    }

    [HttpPost]
    public async Task<IActionResult> GenerateOtp([FromBody] GenerateOtpRequest req)
    {
        var code = await _repository.GenerateAndStoreOtpAsync(req.TransactionId);

        // Dispatch via third-party adapter
        await _emailAdapter.SendEmailAsync(req.Email, "Your OTP Code", $"Your code is: {code}. It expires in 5 minutes.");

        return Created($"/api/otps/{req.TransactionId}", new { ExpiresIn = 300 });
    }

    [HttpPut("{transactionId}")]
    public async Task<IActionResult> VerifyOtp(string transactionId, [FromBody] VerifyOtpRequest req)
    {
        var isValid = await _repository.VerifyAndConsumeOtpAsync(transactionId, req.Code);
        if (!isValid) return BadRequest(new { message = "Invalid, expired, or already used OTP." });

        return NoContent();
    }
}

public record GenerateOtpRequest(string TransactionId, string Email);
public record VerifyOtpRequest(string Code);
