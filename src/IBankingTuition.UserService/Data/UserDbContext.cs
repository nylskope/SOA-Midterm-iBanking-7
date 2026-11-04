using IBankingTuition.UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace IBankingTuition.UserService.Data;

public class UserDbContext : DbContext {
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) {}

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<User>(entity => {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Balance).HasColumnType("numeric(18,2)");
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
            entity.ToTable(t => t.HasCheckConstraint("CHK_PositiveBalance", "\"Balance\" >= 0"));
        });
    }
}