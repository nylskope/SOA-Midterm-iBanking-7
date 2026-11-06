using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace IBankingTuition.PaymentOrchestrator.Data;

public class TransactionDbContext : DbContext {
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options) {}
    public DbSet<TransactionRecord> Transactions { get; set; }
}

public class TransactionRecord {
    [Key]
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FeeId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty;
}
