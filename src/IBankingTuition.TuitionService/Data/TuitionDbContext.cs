using IBankingTuition.TuitionService.Models;
using Microsoft.EntityFrameworkCore;

namespace IBankingTuition.TuitionService.Data;

public class TuitionDbContext : DbContext {
    public TuitionDbContext(DbContextOptions<TuitionDbContext> options) : base(options) {}

    public DbSet<Student> Students { get; set; }
    public DbSet<TuitionFee> TuitionFees { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Student>(entity => {
            entity.HasKey(e => e.Mssv);
        });

        modelBuilder.Entity<TuitionFee>(entity => {
            entity.HasKey(e => e.FeeId);
            entity.HasOne(e => e.Student)
                  .WithMany()
                  .HasForeignKey(e => e.Mssv);

            entity.Property(e => e.Amount).HasColumnType("numeric(18,2)");
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
        });
    }
}
