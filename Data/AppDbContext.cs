using CobolFixedWidthImport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CobolFixedWidthImport.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionLine> TransactionLines => Set<TransactionLine>();
    public DbSet<TransactionFee> TransactionFees => Set<TransactionFee>();
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.RecordType).HasMaxLength(10);
            entity.Property(x => x.AccountNumber).HasMaxLength(20);

            entity.Property(x => x.Amount).HasColumnType("decimal(19,4)");
            entity.Property(x => x.Field3Example).HasColumnType("decimal(19,5)"); // max 5 decimals

            entity.Property(x => x.SourceSystem).HasMaxLength(50);
            entity.Property(x => x.ImportBatchId).HasMaxLength(64);
            entity.Property(x => x.ImportedAtUtc).HasColumnType("datetime2(3)");

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Transaction)
                .HasForeignKey(x => x.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Fees)
                .WithOne(x => x.Transaction)
                .HasForeignKey(x => x.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TransactionLine>(entity =>
        {
            entity.ToTable("TransactionLines");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ItemCode).HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(100);

            entity.Property(x => x.LineAmount).HasColumnType("decimal(19,4)");
            entity.Property(x => x.Quantity).HasColumnType("bigint");

            entity.Property(x => x.ImportedAtUtc).HasColumnType("datetime2(3)");
        });

        modelBuilder.Entity<TransactionFee>(entity =>
        {
            entity.ToTable("TransactionFees");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FeeCode).HasMaxLength(20);
            entity.Property(x => x.Amount).HasColumnType("decimal(19,4)");

            entity.Property(x => x.ImportedAtUtc).HasColumnType("datetime2(3)");
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.AccountNumber).HasMaxLength(20);
            entity.Property(x => x.AccountName).HasMaxLength(200);
            entity.Property(x => x.Balance).HasColumnType("decimal(19,4)");

            entity.Property(x => x.SourceSystem).HasMaxLength(50);
            entity.Property(x => x.ImportBatchId).HasMaxLength(64);
            entity.Property(x => x.ImportedAtUtc).HasColumnType("datetime2(3)");
        });
    }
}
