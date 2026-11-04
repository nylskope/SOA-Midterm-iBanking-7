using System.Text.Json;
using StackExchange.Redis;

namespace IBankingTuition.OtpService.Services;

public class RedisOtpRepository {
    private readonly IDatabase _redis;

    public RedisOtpRepository(IConnectionMultiplexer muxer) {
        _redis = muxer.GetDatabase();
    }

    public async Task<string> GenerateAndStoreOtpAsync(string transactionId) {
        var code = new Random().Next(100000, 999999).ToString();
        var otpData = JsonSerializer.Serialize(new { Code = code, IsUsed = false });
        
        // Native 5-minute expiration (TTL)
        await _redis.StringSetAsync($"otp:{transactionId}", otpData, TimeSpan.FromMinutes(5));
        return code;
    }

    public async Task<bool> VerifyAndConsumeOtpAsync(string transactionId, string code) {
        var key = $"otp:{transactionId}";
        var otpData = await _redis.StringGetAsync(key);
        
        if (!otpData.HasValue) return false; // Expired or doesn't exist

        // ADDED .ToString() to fix the ambiguous call error
        var otp = JsonSerializer.Deserialize<OtpRecord>(otpData.ToString());
        if (otp == null || otp.IsUsed || otp.Code != code) return false;

        // Mark as used (Single-use)
        otp.IsUsed = true;
        await _redis.StringSetAsync(key, JsonSerializer.Serialize(otp), TimeSpan.FromMinutes(5));
        
        return true;
    }
}

public class OtpRecord {
    public string Code { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
}