using IBankingTuition.PaymentOrchestrator.Sagas;
using IBankingTuition.PaymentOrchestrator.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IBankingTuition.PaymentOrchestrator.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionController : ControllerBase
{
    private readonly PaymentSaga _saga;
    private readonly TransactionDbContext _dbContext;

    public TransactionController(PaymentSaga saga, TransactionDbContext dbContext)
    {
        _saga = saga;
        _dbContext = dbContext;
    }

    [HttpPost("init")]
    public async Task<IActionResult> InitTransaction([FromBody] InitTransactionRequest req)
    {
        // 1. Prevent the SAME user from creating multiple PENDING transactions for the SAME fee.
        // (By checking t.UserId == req.UserId, DIFFERENT users can still initiate the same fee concurrently for Scenario 3)
        var isAlreadyPending = await _dbContext.Transactions
            .AnyAsync(t => t.FeeId == req.FeeId && t.UserId == req.UserId && t.Status == "PENDING");

        if (isAlreadyPending)
            return BadRequest(new { message = "Bạn đang có một giao dịch chờ xử lý cho khoản học phí này. Vui lòng hoàn tất hoặc hủy giao dịch cũ trong Lịch sử." });

        var txn = new TransactionRecord {
            TransactionId = req.TransactionId,
            UserId = req.UserId,
            FeeId = req.FeeId,
            Amount = req.Amount,
            Status = "PENDING"
        };

        _dbContext.Transactions.Add(txn);
        await _dbContext.SaveChangesAsync();

        return Ok(txn);
    }

    [HttpPost("cancel/{transactionId}")]
    public async Task<IActionResult> CancelTransaction(string transactionId)
    {
        var txn = await _dbContext.Transactions.FindAsync(transactionId);
        if (txn == null) return NotFound();

        // Only allow canceling if it hasn't been finalized yet
        if (txn.Status == "PENDING") {
            txn.Status = "CANCELED";
            await _dbContext.SaveChangesAsync();
        }

        return Ok(txn);
    }

    [HttpPost("pay")]
    public async Task<IActionResult> PayTuition([FromBody] PaymentCommand command)
    {
        var result = await _saga.ExecuteAsync(command);
        if (!result.Success) return BadRequest(new { message = result.Message });
        return Ok(result);
    }

    [HttpGet("history/{userId}")]
    public async Task<IActionResult> GetHistory(string userId)
    {
        var history = await _dbContext.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return Ok(history);
    }
}

public record InitTransactionRequest(string TransactionId, string UserId, string FeeId, decimal Amount);
