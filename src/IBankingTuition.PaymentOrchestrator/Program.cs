using IBankingTuition.PaymentOrchestrator.Sagas;
using IBankingTuition.PaymentOrchestrator.Data;
using IBankingTuition.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TransactionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IPaymentAdapter, VNPayPaymentAdapter>();
builder.Services.AddHttpClient<IEmailAdapter, BrevoEmailAdapter>();
builder.Services.AddScoped<PaymentSaga>();

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
