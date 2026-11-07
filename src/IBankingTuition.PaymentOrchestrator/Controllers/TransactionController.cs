using IBankingTuition.Infrastructure.Adapters;
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
    private readonly IConfiguration _config;
    private readonly TransactionDbContext _dbContext;

    public TransactionController(PaymentSaga saga, IConfiguration config, TransactionDbContext dbContext)
    {
        _saga = saga;
        _config = config;
        _dbContext = dbContext;
    }

    [HttpPost("pay")]
    public async Task<IActionResult> PayTuition([FromBody] PaymentCommand command)
    {
        var result = await _saga.ExecuteAsync(command);
        if (!result.Success) return BadRequest(new { message = result.Message });
        return Ok(result);
    }

    [HttpGet("return")]
    public async Task<IActionResult> VNPayReturn(
            [FromQuery] string vnp_TxnRef,
            [FromQuery] int userRowVersion,
            [FromQuery] int tuitionRowVersion,
            [FromQuery] string email)
    {
        var query = Request.Query;
        var hashSecret = _config["VNPay:HashSecret"] ?? throw new ArgumentNullException("VNPay HashSecret missing");
        var vnpay = new VnPayLibrary();

        foreach (var kvp in query)
        {
            if (!string.IsNullOrEmpty(kvp.Key) && kvp.Key.StartsWith("vnp_"))
            {
                vnpay.AddResponseData(kvp.Key, kvp.Value.ToString());
            }
        }

        var vnp_SecureHash = Request.Query["vnp_SecureHash"].ToString();
        bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, hashSecret);

        if (checkSignature)
        {
            var responseCode = vnpay.GetResponseData("vnp_ResponseCode");

            if (responseCode == "00" && vnpay.GetResponseData("vnp_TransactionStatus") == "00")
            {
                var finalizationSuccess = await _saga.CompletePaymentAsync(vnp_TxnRef, userRowVersion, tuitionRowVersion, email);
                if (finalizationSuccess)
                {
                    return Redirect($"http://localhost:3000/?status=success&txn={vnp_TxnRef}");
                }
                else
                {
                    return Redirect($"http://localhost:3000/?status=conflict&txn={vnp_TxnRef}");
                }
            }
            else if (responseCode == "24")
            {
                // 24 explicitly means the user clicked 'Hủy thanh toán'
                await _saga.UpdateTransactionStatusAsync(vnp_TxnRef, "CANCELED");
                return Redirect($"http://localhost:3000/?status=canceled&txn={vnp_TxnRef}");
            }
            else
            {
                // Any other VNPAY error
                await _saga.UpdateTransactionStatusAsync(vnp_TxnRef, "FAILED");
                return Redirect($"http://localhost:3000/?status=failed&txn={vnp_TxnRef}");
            }
        }

        await _saga.UpdateTransactionStatusAsync(vnp_TxnRef, "FAILED");
        return Redirect("http://localhost:3000/?status=invalid_signature");
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

    [HttpPost("fail-orphaned/{transactionId}")]
    public async Task<IActionResult> FailOrphanedTransaction(string transactionId)
    {
        var result = await _saga.UpdateTransactionStatusAsync(transactionId, "FAILED");
        if (result) return Ok();
        return BadRequest(new { message = "Transaction not found or not in PENDING state." });
    }
}
