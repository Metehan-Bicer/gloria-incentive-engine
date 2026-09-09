using System.Text;
using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Import;
using Gloria.Incentive.Api.Import.Parsers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gloria.Incentive.Tests.Import;

public class CsvImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly CsvImportService _service;

    private const string PmsHeader = "BelgeNo;IslemTarihi;OdaNo;MisafirAdi;UrunKodu;UrunAdi;Adet;Tutar;ParaBirimi;KasiyerNo;IslemTipi;Otel";

    public CsvImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var spa = new Department { Name = "SPA" };
        _db.Employees.Add(new Employee { EmployeeNo = "P1001", FullName = "Ayşe Demir", Department = spa, HotelCode = "GSR", HireDate = new DateOnly(2024, 3, 1) });
        _db.SaveChanges();

        _service = new CsvImportService(_db, [new PmsParser(), new PosParser(), new ErpParser()], new TestUser(), NullLogger<CsvImportService>.Instance);
    }

    [Fact]
    public async Task Imports_valid_rows_and_logs_errors_separately()
    {
        var csv = string.Join('\n', PmsHeader,
            "180001;2026-08-10;101;Guest;SPA_MSJ60;SPA Masaj 60dk;1;2800.00;TRY;P1001;POSTING;GSR",
            "180002;32/08/2026;101;Guest;SPA_MSJ60;SPA Masaj 60dk;1;2800.00;TRY;P1001;POSTING;GSR",
            "180003;2026-08-11;101;Guest;SPA_MSJ60;SPA Masaj 60dk;1;2.500,00;TRY;P1001;POSTING;GSR",
            "180004;2026-08-11;101;Guest;SPA_MSJ60;SPA Masaj 60dk;1;2800.00;TRY;P9999;POSTING;GSR",
            "180005;2026-08-12;101;Guest;SPA_MSJ90;SPA Masaj 90dk;1;-3900.00;TRY;P1001;REVERSAL;GSR");

        var result = await _service.ImportAsync(SourceSystem.PMS, "test.csv", Stream(csv));

        Assert.Equal(5, result.Batch.TotalRows);
        Assert.Equal(3, result.Batch.ImportedRows);
        Assert.Equal(2, result.Batch.ErrorRows);
        Assert.Equal(0, result.Batch.DuplicateRows);

        Assert.Contains(result.Errors, e => e.LineNumber == 3 && e.Reason.Contains("geçersiz tarih"));
        Assert.Contains(result.Errors, e => e.LineNumber == 5 && e.Reason.Contains("P9999"));

        var refund = await _db.SaleRecords.SingleAsync(s => s.ExternalDocumentNo == "180005");
        Assert.True(refund.IsRefund);
        Assert.Equal(3900m, refund.Amount);

        var turkishFormat = await _db.SaleRecords.SingleAsync(s => s.ExternalDocumentNo == "180003");
        Assert.Equal(2500m, turkishFormat.Amount);
    }

    [Fact]
    public async Task Duplicates_within_file_and_across_imports_are_skipped_and_logged()
    {
        var csv = string.Join('\n', PmsHeader,
            "180001;2026-08-10;101;Guest;SPA_MSJ60;SPA Masaj 60dk;1;2800.00;TRY;P1001;POSTING;GSR",
            "180001;2026-08-10;101;Guest;SPA_MSJ60;SPA Masaj 60dk;1;2800.00;TRY;P1001;POSTING;GSR");

        var first = await _service.ImportAsync(SourceSystem.PMS, "first.csv", Stream(csv));
        var second = await _service.ImportAsync(SourceSystem.PMS, "second.csv", Stream(csv));

        Assert.Equal(1, first.Batch.ImportedRows);
        Assert.Equal(1, first.Batch.DuplicateRows);
        Assert.Equal(0, second.Batch.ImportedRows);
        Assert.Equal(2, second.Batch.DuplicateRows);
        Assert.All(second.Errors, e => Assert.True(e.IsDuplicate));
        Assert.Equal(1, await _db.SaleRecords.CountAsync());
    }

    [Fact]
    public async Task Rows_in_closed_period_are_rejected()
    {
        _db.Periods.Add(new Period { Year = 2026, Month = 8, Status = PeriodStatus.Closed, ClosedAt = DateTime.UtcNow, ClosedBy = "test" });
        await _db.SaveChangesAsync();

        var csv = string.Join('\n', PmsHeader,
            "180001;2026-08-10;101;Guest;SPA_MSJ60;SPA Masaj 60dk;1;2800.00;TRY;P1001;POSTING;GSR");

        var result = await _service.ImportAsync(SourceSystem.PMS, "closed.csv", Stream(csv));

        Assert.Equal(0, result.Batch.ImportedRows);
        Assert.Equal(1, result.Batch.ErrorRows);
        Assert.Contains("kapatılmış", result.Errors.Single().Reason);
    }

    private static MemoryStream Stream(string content) => new(Encoding.UTF8.GetBytes(content));

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private class TestUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public string Role => Roles.Admin;
        public string? EmployeeNo => null;
        public string DisplayName => "test";
        public bool IsInRole(string role) => role == Roles.Admin;
    }
}
