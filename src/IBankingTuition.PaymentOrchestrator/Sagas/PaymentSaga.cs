using IBankingTuition.Infrastructure.Adapters;
using IBankingTuition.PaymentOrchestrator.Data;
using Microsoft.Extensions.Configuration;

namespace IBankingTuition.PaymentOrchestrator.Sagas;

public class PaymentSaga
{
    private readonly HttpClient _internalClient;
    private readonly IEmailAdapter _emailAdapter;
    private readonly TransactionDbContext _dbContext;
    private readonly IConfiguration _config;

    public PaymentSaga(HttpClient internalClient, IEmailAdapter emailAdapter, TransactionDbContext dbContext, IConfiguration config)
    {
        _internalClient = internalClient;
        _emailAdapter = emailAdapter;
        _dbContext = dbContext;
        _config = config;
    }

    public async Task<SagaResult> ExecuteAsync(PaymentCommand cmd)
    {
        var otpUrl = _config["InternalServices:OtpService"] ?? "http://localhost:5006";
        var userUrl = _config["InternalServices:UserService"] ?? "http://localhost:5174";
        var tuitionUrl = _config["InternalServices:TuitionService"] ?? "http://localhost:5132";

        // Fetch the PENDING transaction initialized by the frontend
        var txn = await _dbContext.Transactions.FindAsync(cmd.TransactionId);
        if (txn == null)
        {
            return new SagaResult(false, "Không tìm thấy giao dịch hợp lệ.");
        }

        // 1. Verify OTP
        var otpRes = await _internalClient.PutAsJsonAsync($"{otpUrl}/api/otps/{cmd.TransactionId}", new { Code = cmd.OtpCode });
        if (!otpRes.IsSuccessStatusCode)
        {
            return new SagaResult(false, "Xác thực OTP thất bại hoặc OTP đã hết hạn.");
        }

        // 2. SAGA PHASE 1: Deduct Balance (Optimistic Concurrency Control)
        var deductRes = await _internalClient.PostAsJsonAsync($"{userUrl}/api/users/{cmd.PayerId}/deduct", new { Amount = cmd.Amount, RowVersion = cmd.UserRowVersion });
        if (!deductRes.IsSuccessStatusCode)
        {
            txn.Status = "FAILED";
            await _dbContext.SaveChangesAsync();
            return new SagaResult(false, "Giao dịch thất bại: Số dư không đủ hoặc dữ liệu tài khoản đã bị thay đổi (Conflict).");
        }

        // 3. SAGA PHASE 2: Mark Tuition as Paid (Optimistic Concurrency Control)
        var tuitionRes = await _internalClient.PutAsJsonAsync($"{tuitionUrl}/api/tuitions/{cmd.FeeId}", new { IsPaid = true, RowVersion = cmd.TuitionRowVersion });
        if (!tuitionRes.IsSuccessStatusCode)
        {
            // COMPENSATING TRANSACTION: Tuition update failed. We must refund the user.
            await _internalClient.PostAsJsonAsync($"{userUrl}/api/users/{cmd.PayerId}/refund", new { Amount = cmd.Amount });

            txn.Status = "COMPENSATED";
            await _dbContext.SaveChangesAsync();
            return new SagaResult(false, "Giao dịch bị hoàn tác (Refunded): Học phí này đã được thanh toán bởi một giao dịch khác.");
        }

        // 4. Success Finalization
        txn.Status = "SUCCESS";
        await _dbContext.SaveChangesAsync();

        _ = _emailAdapter.SendEmailAsync(cmd.Email, "Thanh toán học phí thành công", $"Giao dịch {cmd.TransactionId} thanh toán thành công {cmd.Amount} VND cho mã học phí {cmd.FeeId}.");

        return new SagaResult(true, "Thanh toán thành công!");
    }
}

public record PaymentCommand(string TransactionId, string PayerId, string FeeId, decimal Amount, string OtpCode, int UserRowVersion, int TuitionRowVersion, string Email);
public record SagaResult(bool Success, string Message);
