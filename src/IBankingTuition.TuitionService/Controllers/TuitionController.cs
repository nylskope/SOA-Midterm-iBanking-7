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
    public async Task<IActionResult> GetTuition(string mssv)
    {
        var studentFees = await _context.TuitionFees
            .Include(f => f.Student)
            .Where(f => f.Mssv == mssv && !f.IsPaid)
            .Select(f => new
            {
                f.FeeId,
                f.Mssv,
                f.Student.FullName,
                f.Amount,
                f.IsPaid,
                f.RowVersion
            })
            .ToListAsync();

        if (studentFees.Count == 0) return NotFound(new { message = "No pending tuition found for this MSSV." });

        return Ok(studentFees);
    }

    [HttpGet("fee/{feeId}")]
    public async Task<IActionResult> GetSpecificTuition(string feeId) {
        var fee = await _context.TuitionFees
            .Include(f => f.Student)
            .Where(f => f.FeeId == feeId)
            .Select(f => new {
                f.FeeId,
                f.Mssv,
                f.Student.FullName,
                f.Amount,
                f.IsPaid,
                f.RowVersion
            })
            .FirstOrDefaultAsync();

        if (fee == null) return NotFound(new { message = "Tuition fee not found." });

        return Ok(fee);
    }

    [HttpPut("{feeId}")]
    public async Task<IActionResult> MarkTuitionPaid(string feeId, [FromBody] MarkPaidRequest req) {
        var fee = await _context.TuitionFees.FindAsync(feeId);
        if (fee == null) return NotFound();
        if (fee.IsPaid) return BadRequest("Fee is already paid.");

        if (fee.RowVersion != req.RowVersion)
            return Conflict("Concurrency conflict. Fee was updated by another process.");

        fee.IsPaid = true;
        fee.RowVersion += 1;

        try {
            await _context.SaveChangesAsync();
            return NoContent();
        } catch (DbUpdateConcurrencyException) {
            return Conflict("Concurrency conflict occurred during save.");
        }
    }
}

public record MarkPaidRequest(bool IsPaid, int RowVersion);
