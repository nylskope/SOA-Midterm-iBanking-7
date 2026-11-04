using IBankingTuition.TuitionService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IBankingTuition.TuitionService.Controllers;

[ApiController]
[Route("api/tuitions")]
public class TuitionController : ControllerBase {
    private readonly TuitionDbContext _context;

    public TuitionController(TuitionDbContext context) {
        _context = context;
    }

    [HttpGet("{mssv}")]
    public async Task<IActionResult> GetTuition(string mssv) {
        var studentFee = await _context.TuitionFees
            .Include(f => f.Student)
            .Where(f => f.Mssv == mssv && !f.IsPaid)
            .Select(f => new {
                f.FeeId,
                f.Mssv,
                f.Student.FullName,
                f.Amount,
                f.IsPaid,
                f.RowVersion
            })
            .FirstOrDefaultAsync();

        if (studentFee == null) return NotFound(new { message = "No pending tuition found for this MSSV." });

        return Ok(studentFee);
    }
}