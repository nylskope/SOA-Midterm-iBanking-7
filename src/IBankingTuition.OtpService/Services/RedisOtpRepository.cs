using System.Text.Json;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace IBankingTuition.OtpService.Services;

public class RedisOtpRepository
{
    private readonly IDatabase _redis;

    public RedisOtpRepository(IConnectionMultiplexer muxer)
    {
        _redis = muxer.GetDatabase();
    }

    public async Task<string> GenerateAndStoreOtpAsync(string transactionId)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otpData = JsonSerializer.Serialize(new { Code = code, IsUsed = false });

        await _redis.StringSetAsync($"otp:{transactionId}", otpData, TimeSpan.FromMinutes(5));
        return code;
    }

    public async Task<bool> VerifyAndConsumeOtpAsync(string transactionId, string code)
    {
        var key = $"otp:{transactionId}";
        var otpData = await _redis.StringGetAsync(key);

        if (!otpData.HasValue) return false;

        var otp = JsonSerializer.Deserialize<OtpRecord>(otpData.ToString());
        if (otp == null || otp.IsUsed || otp.Code != code) return false;

        otp.IsUsed = true;
        await _redis.StringSetAsync(key, JsonSerializer.Serialize(otp), TimeSpan.FromMinutes(5));

        return true;
    }
}

public class OtpRecord
{
    public string Code { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
}
