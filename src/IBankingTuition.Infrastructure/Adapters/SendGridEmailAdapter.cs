using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace IBankingTuition.Infrastructure.Adapters;

public class SendGridEmailAdapter : IEmailAdapter 
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public SendGridEmailAdapter(HttpClient httpClient, IConfiguration config) 
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body) 
    {
        var apiKey = _config["SendGrid:ApiKey"] ?? throw new ArgumentNullException("SendGrid API Key is missing");
        var fromEmail = _config["SendGrid:FromEmail"] ?? "noreply@ibanking.tdtu.edu.vn";
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new {
            personalizations = new[] { new { to = new[] { new { email = toEmail } } } },
            from = new { email = fromEmail },
            subject = subject,
            content = new[] { new { type = "text/plain", value = body } }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        
        // Calling the external SendGrid REST API
        var response = await _httpClient.PostAsync("https://api.sendgrid.com/v3/mail/send", content);
        
        response.EnsureSuccessStatusCode();
    }
}