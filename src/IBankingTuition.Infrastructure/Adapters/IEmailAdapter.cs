namespace IBankingTuition.Infrastructure.Adapters;

public interface IEmailAdapter {
    Task SendEmailAsync(string toEmail, string subject, string body);
}