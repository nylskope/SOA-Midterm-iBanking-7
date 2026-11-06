using StackExchange.Redis;
using IBankingTuition.OtpService.Services;
using IBankingTuition.Infrastructure.Adapters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("RedisConnection") ?? "localhost:6379"));

builder.Services.AddScoped<RedisOtpRepository>();
builder.Services.AddHttpClient<IEmailAdapter, BrevoEmailAdapter>();

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
