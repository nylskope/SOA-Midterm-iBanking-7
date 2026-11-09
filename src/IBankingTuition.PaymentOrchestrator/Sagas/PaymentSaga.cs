using IBankingTuition.Infrastructure.Adapters;
using IBankingTuition.PaymentOrchestrator.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IBankingTuition.PaymentOrchestrator.Sagas;

public class PaymentSaga
{
    private readonly HttpClient _internalClient;
    private readonly IPaymentAdapter _paymentAdapter;
    private readonly IEmailAdapter _emailAdapter;
    private readonly TransactionDbContext _dbContext;
    private readonly IConfiguration _config;

    public PaymentSaga(HttpClient internalClient, IPaymentAdapter paymentAdapter, IEmailAdapter emailAdapter, TransactionDbContext dbContext, IConfiguration config)
    {
        _internalClient = internalClient;
        _paymentAdapter = paymentAdapter;
        _emailAdapter = emailAdapter;
        _dbContext = dbContext;
        _config = config;
    }

    public async Task<SagaResult> ExecuteAsync(PaymentCommand cmd)
    {
        var otpUrl = _config["InternalServices:OtpService"] ?? "http://localhost:5006";

        var otpRes = await _internalClient.PutAsJsonAsync($"{otpUrl}/api/otps/{cmd.TransactionId}", new { Code = cmd.OtpCode });
        if (!otpRes.IsSuccessStatusCode)
        {
            return new SagaResult(false, "OTP Verification Failed.");
        }

        var txn = new TransactionRecord
        {
            TransactionId = cmd.TransactionId,
            UserId = cmd.PayerId,
            FeeId = cmd.FeeId,
            Amount = cmd.Amount,
            Status = "PENDING"
        };
        _dbContext.Transactions.Add(txn);
        await _dbContext.SaveChangesAsync();

        var paymentUrl = await _paymentAdapter.GeneratePaymentUrlAsync(
            cmd.TransactionId,
            cmd.Amount,
            $"Tuition {cmd.FeeId} - {cmd.TransactionId}",
            cmd.UserRowVersion,
            cmd.TuitionRowVersion,
            cmd.Email
        );

        return new SagaResult(true, "Transaction Initiated", paymentUrl);
    }

    public async Task<bool> CompletePaymentAsync(string transactionId, int userRowVersion, int tuitionRowVersion, string email)
    {
        var txn = await _dbContext.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId);
        if (txn == null || txn.Status != "PENDING") return false;

        var userUrl = _config["InternalServices:UserService"] ?? "http://localhost:5174";
        var tuitionUrl = _config["InternalServices:TuitionService"] ?? "http://localhost:5132";

        var balanceRes = await _internalClient.GetFromJsonAsync<JsonElement>($"{userUrl}/api/users/{txn.UserId}/balance");
        var freshUserRowVersion = balanceRes.GetProperty("rowVersion").GetInt32();

        var deductRes = await _internalClient.PostAsJsonAsync($"{userUrl}/api/users/{txn.UserId}/deduct", new { Amount = txn.Amount, RowVersion = freshUserRowVersion });
        if (!deductRes.IsSuccessStatusCode)
        {
            txn.Status = "FAILED";
            await _dbContext.SaveChangesAsync();
            return false;
        }

        var tuitionRes = await _internalClient.PutAsJsonAsync($"{tuitionUrl}/api/tuitions/{txn.FeeId}", new { IsPaid = true, RowVersion = tuitionRowVersion });
        if (!tuitionRes.IsSuccessStatusCode)
        {
            await _internalClient.PostAsJsonAsync($"{userUrl}/api/users/{txn.UserId}/refund", new { Amount = txn.Amount });
            txn.Status = "COMPENSATED";
            await _dbContext.SaveChangesAsync();
            return false;
        }

        txn.Status = "SUCCESS";
        await _dbContext.SaveChangesAsync();
        _ = _emailAdapter.SendEmailAsync(email, "Payment Successful", $"Transaction {transactionId} completed for {txn.Amount} VND.");

        return true;
    }

    public async Task<bool> UpdateTransactionStatusAsync(string transactionId, string newStatus)
    {
        var txn = await _dbContext.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId);
        if (txn != null && txn.Status == "PENDING")
        {
            txn.Status = newStatus;
            await _dbContext.SaveChangesAsync();
            return true;
        }
        return false;
    }
}

public record PaymentCommand(string TransactionId, string PayerId, string FeeId, decimal Amount, string OtpCode, int UserRowVersion, int TuitionRowVersion, string Email);
public record SagaResult(bool Success, string Message, string? PaymentUrl = null);
