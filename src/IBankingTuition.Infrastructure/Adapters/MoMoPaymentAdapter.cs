using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace IBankingTuition.Infrastructure.Adapters;

public class MoMoPaymentAdapter : IPaymentAdapter {
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public MoMoPaymentAdapter(HttpClient httpClient, IConfiguration config) {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<string> GeneratePaymentUrlAsync(string transactionId, decimal amount, string orderInfo) {
        var endpoint = _config["MoMo:Endpoint"];
        var partnerCode = _config["MoMo:PartnerCode"];
        var accessKey = _config["MoMo:AccessKey"];
        var secretKey = _config["MoMo:SecretKey"];
        var returnUrl = _config["MoMo:ReturnUrl"];
        var notifyUrl = _config["MoMo:NotifyUrl"];

        var requestData = new {
            partnerCode,
            accessKey,
            requestId = Guid.NewGuid().ToString(),
            amount = amount.ToString(),
            orderId = transactionId,
            orderInfo,
            returnUrl,
            notifyUrl,
            requestType = "captureMoMoWallet"
        };

        var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<JsonElement>(responseString);

        return responseData.GetProperty("payUrl").GetString() ?? throw new Exception("Payment URL generation failed.");
    }
}