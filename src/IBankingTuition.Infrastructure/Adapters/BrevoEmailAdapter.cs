using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace IBankingTuition.Infrastructure.Adapters;

public class BrevoEmailAdapter : IEmailAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public BrevoEmailAdapter(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var apiKey = _config["Brevo:ApiKey"] ?? throw new ArgumentNullException("Brevo API Key is missing");
        var fromEmail = _config["Brevo:FromEmail"] ?? "noreply@ibanking.local";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("accept", "application/json");

        var payload = new {
            sender = new { email = fromEmail, name = "SOA iBanking System" },
            to = new[] { new { email = toEmail } },
            subject = subject,
            textContent = body
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.brevo.com/v3/smtp/email", content);
        response.EnsureSuccessStatusCode();
    }
}
