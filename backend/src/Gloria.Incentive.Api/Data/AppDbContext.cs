using Gloria.Incentive.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gloria.Incentive.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<SaleRecord> SaleRecords => Set<SaleRecord>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<CommissionCalculation> CommissionCalculations => Set<CommissionCalculation>();
    public DbSet<CommissionCalculationLine> CommissionCalculationLines => Set<CommissionCalculationLine>();
    public DbSet<Period> Periods => Set<Period>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Employee>(b =>
        {
            b.Property(x => x.EmployeeNo).HasMaxLength(20).IsRequired();
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.HotelCode).HasMaxLength(10).IsRequired();
            b.HasIndex(x => x.EmployeeNo).IsUnique();
            b.HasOne(x => x.Department).WithMany(d => d.Employees).HasForeignKey(x => x.DepartmentId);
        });

        modelBuilder.Entity<SaleRecord>(b =>
        {
            b.Property(x => x.ExternalDocumentNo).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            b.Property(x => x.ProductCategory).HasMaxLength(50).IsRequired();
            b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            b.Property(x => x.RefundReference).HasMaxLength(50);
            b.Property(x => x.HotelCode).HasMaxLength(10).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.HasIndex(x => new { x.SourceSystem, x.ExternalDocumentNo, x.ProductCode }).IsUnique();
            b.HasIndex(x => new { x.EmployeeId, x.TransactionDate });
            b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            b.HasOne(x => x.ImportBatch).WithMany().HasForeignKey(x => x.ImportBatchId);
            b.Ignore(x => x.SignedAmount);
        });

        modelBuilder.Entity<CommissionRule>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.ParametersJson).IsRequired();
            b.Property(x => x.ProductCategory).HasMaxLength(50);
            b.Property(x => x.ProductCode).HasMaxLength(50);
            b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId);
        });

        modelBuilder.Entity<CommissionCalculation>(b =>
        {
            b.Property(x => x.GrossSales).HasPrecision(18, 2);
            b.Property(x => x.RefundTotal).HasPrecision(18, 2);
            b.Property(x => x.TotalCommission).HasPrecision(18, 2);
            b.Property(x => x.CalculatedBy).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.EmployeeId, x.Year, x.Month }).IsUnique();
            b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            b.HasMany(x => x.Lines).WithOne(l => l.Calculation).HasForeignKey(l => l.CalculationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommissionCalculationLine>(b =>
        {
            b.Property(x => x.RuleName).HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.BaseAmount).HasPrecision(18, 2);
            b.Property(x => x.Rate).HasPrecision(9, 4);
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Period>(b =>
        {
            b.Property(x => x.ClosedBy).HasMaxLength(100);
            b.HasIndex(x => new { x.Year, x.Month }).IsUnique();
            b.Ignore(x => x.IsClosed);
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            b.Property(x => x.EntityId).HasMaxLength(50).IsRequired();
            b.Property(x => x.ChangedBy).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.EntityName, x.EntityId });
        });

        modelBuilder.Entity<ImportBatch>(b =>
        {
            b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            b.Property(x => x.ImportedBy).HasMaxLength(100).IsRequired();
            b.HasMany(x => x.Errors).WithOne(e => e.Batch).HasForeignKey(e => e.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportError>(b =>
        {
            b.Property(x => x.RawLine).HasMaxLength(2000).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        });
    }
}

public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
