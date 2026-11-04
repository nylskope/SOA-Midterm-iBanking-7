namespace IBankingTuition.Infrastructure.Adapters;

public interface IPaymentAdapter {
    Task<string> GeneratePaymentUrlAsync(string transactionId, decimal amount, string orderInfo);
}