using IBankingTuition.Infrastructure.Adapters;

namespace IBankingTuition.PaymentOrchestrator.Sagas;

public class PaymentSaga {
    private readonly HttpClient _internalClient;
    private readonly IPaymentAdapter _momoAdapter;
    private readonly IEmailAdapter _emailAdapter;

    public PaymentSaga(HttpClient internalClient, IPaymentAdapter momoAdapter, IEmailAdapter emailAdapter) {
        _internalClient = internalClient;
        _momoAdapter = momoAdapter;
        _emailAdapter = emailAdapter;
    }

    public async Task<SagaResult> ExecuteAsync(PaymentCommand cmd) {
        // 1. Verify OTP
        var otpRes = await _internalClient.PutAsJsonAsync($"http://otp-service/api/otps/{cmd.TransactionId}", new { cmd.OtpCode });
        if (!otpRes.IsSuccessStatusCode) return new SagaResult(false, "OTP Verification Failed.");

        // 2. Deduct Balance (User Service)
        var deductRes = await _internalClient.PostAsJsonAsync($"http://user-service/api/users/{cmd.PayerId}/deduct", new { cmd.Amount, cmd.UserRowVersion });
        if (!deductRes.IsSuccessStatusCode) return new SagaResult(false, "Balance Deduction Failed (Concurrency or Insufficient Funds).");

        // 3. Mark Tuition Paid (Tuition Service)
        var tuitionRes = await _internalClient.PutAsJsonAsync($"http://tuition-service/api/tuitions/{cmd.FeeId}", new { IsPaid = true, cmd.TuitionRowVersion });
        if (!tuitionRes.IsSuccessStatusCode) {
            // COMPENSATION TRIGGERED: Refund User
            await _internalClient.PostAsJsonAsync($"http://user-service/api/users/{cmd.PayerId}/refund", new { cmd.Amount });
            return new SagaResult(false, "Tuition Update Failed. Funds Refunded.");
        }

        // 4. Generate MoMo Link for Ledger tracking (Integration)
        var paymentUrl = await _momoAdapter.GeneratePaymentUrlAsync(cmd.TransactionId, cmd.Amount, $"Tuition {cmd.FeeId}");

        // 5. Send Success Email
        _ = _emailAdapter.SendEmailAsync(cmd.Email, "Payment Successful", $"Transaction {cmd.TransactionId} completed. Details: {paymentUrl}");

        return new SagaResult(true, "Success", paymentUrl);
    }
}

public record PaymentCommand(string TransactionId, string PayerId, string FeeId, decimal Amount, string OtpCode, int UserRowVersion, int TuitionRowVersion, string Email);
public record SagaResult(bool Success, string Message, string? PaymentUrl = null);