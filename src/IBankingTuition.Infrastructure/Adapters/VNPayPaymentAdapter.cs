using System.Text;
using Microsoft.Extensions.Configuration;

namespace IBankingTuition.Infrastructure.Adapters;

public class VNPayPaymentAdapter : IPaymentAdapter {
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public VNPayPaymentAdapter(HttpClient httpClient, IConfiguration config) {
        _httpClient = httpClient;
        _config = config;
    }

    public Task<string> GeneratePaymentUrlAsync(string transactionId, decimal amount, string orderInfo, int userRowVersion, int tuitionRowVersion, string email) {
        var tmnCode = _config["VNPay:TmnCode"] ?? throw new ArgumentNullException("VNPay TmnCode missing");
        var hashSecret = _config["VNPay:HashSecret"] ?? throw new ArgumentNullException("VNPay HashSecret missing");
        var baseUrl = _config["VNPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var configuredReturn = _config["VNPay:ReturnUrl"] ?? "http://localhost:5004/api/transactions/return";
        var returnUrl = $"{configuredReturn}?email={Uri.EscapeDataString(email)}&userRowVersion={userRowVersion}&tuitionRowVersion={tuitionRowVersion}";

        var vnpay = new VnPayLibrary();

        vnpay.AddRequestData("vnp_Version", "2.1.0");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_TmnCode", tmnCode);
        vnpay.AddRequestData("vnp_Amount", (amount * 100).ToString("0"));
        vnpay.AddRequestData("vnp_CreateDate", DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", "VND");
        vnpay.AddRequestData("vnp_IpAddr", "127.0.0.1");
        vnpay.AddRequestData("vnp_Locale", "vn");
        vnpay.AddRequestData("vnp_OrderInfo", orderInfo);
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnpay.AddRequestData("vnp_TxnRef", transactionId);

        var paymentUrl = vnpay.CreateRequestUrl(baseUrl, hashSecret);

        return Task.FromResult(paymentUrl);
    }
}
