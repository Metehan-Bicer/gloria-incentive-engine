using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Import.Parsers;
using Gloria.Incentive.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Import;

public class CsvImportService
{
    private readonly AppDbContext _db;
    private readonly IReadOnlyDictionary<SourceSystem, ISourceParser> _parsers;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CsvImportService> _logger;

    public CsvImportService(AppDbContext db, IEnumerable<ISourceParser> parsers, ICurrentUser currentUser, ILogger<CsvImportService> logger)
    {
        _db = db;
        _parsers = parsers.ToDictionary(p => p.Source);
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ImportResultDto> ImportAsync(SourceSystem source, string fileName, Stream content, CancellationToken ct = default)
    {
        if (!_parsers.TryGetValue(source, out var parser))
            throw new BadRequestException($"Desteklenmeyen kaynak sistem: {source}");

        var batch = new ImportBatch
        {
            SourceSystem = source,
            FileName = fileName,
            StartedAt = DateTime.UtcNow,
            ImportedBy = _currentUser.DisplayName
        };
        _db.ImportBatches.Add(batch);

        var employees = await _db.Employees.ToDictionaryAsync(e => e.EmployeeNo, StringComparer.OrdinalIgnoreCase, ct);
        var closedPeriods = await _db.Periods
            .Where(p => p.Status == PeriodStatus.Closed)
            .Select(p => new { p.Year, p.Month })
            .ToListAsync(ct);
        var closedSet = closedPeriods.Select(p => (p.Year, p.Month)).ToHashSet();

        var existingKeys = (await _db.SaleRecords
                .Where(s => s.SourceSystem == source)
                .Select(s => new { s.ExternalDocumentNo, s.ProductCode })
                .ToListAsync(ct))
            .Select(k => DedupKey(k.ExternalDocumentNo, k.ProductCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            DetectColumnCountChanges = false
        };

        using var reader = new StreamReader(content);
        using var csv = new CsvReader(reader, config);

        if (!await csv.ReadAsync() || !csv.ReadHeader())
            throw new BadRequestException("Dosya boş ya da başlık satırı okunamadı.");

        var headers = csv.HeaderRecord ?? [];
        var missing = parser.RequiredHeaders.Where(h => !headers.Contains(h, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
            throw new BadRequestException($"{source} dosyasında beklenen sütunlar eksik: {string.Join(", ", missing)}");

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            var lineNumber = csv.Parser.Row;
            var rawLine = (csv.Parser.RawRecord ?? string.Empty).TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            batch.TotalRows++;

            try
            {
                var parsed = parser.Parse(csv);

                if (!employees.TryGetValue(parsed.EmployeeNo, out var employee))
                    throw new RowParseException($"Personel listesinde bulunamadı: {parsed.EmployeeNo}");

                if (closedSet.Contains((parsed.TransactionDate.Year, parsed.TransactionDate.Month)))
                    throw new RowParseException($"{parsed.TransactionDate:yyyy-MM} dönemi kapatılmış, kayıt eklenemez");

                var key = DedupKey(parsed.ExternalDocumentNo, parsed.ProductCode);
                if (!existingKeys.Add(key))
                {
                    batch.DuplicateRows++;
                    batch.Errors.Add(new ImportError
                    {
                        LineNumber = lineNumber,
                        RawLine = rawLine,
                        Reason = $"Mükerrer kayıt: {source}-{parsed.ExternalDocumentNo} / {parsed.ProductCode}",
                        IsDuplicate = true
                    });
                    continue;
                }

                _db.SaleRecords.Add(new SaleRecord
                {
                    SourceSystem = source,
                    ExternalDocumentNo = parsed.ExternalDocumentNo,
                    TransactionDate = parsed.TransactionDate,
                    ProductCode = parsed.ProductCode,
                    ProductName = parsed.ProductName,
                    ProductCategory = ProductCatalog.CategoryFor(parsed.ProductCode),
                    Quantity = parsed.Quantity,
                    Amount = parsed.Amount,
                    Currency = parsed.Currency,
                    IsRefund = parsed.IsRefund,
                    RefundReference = parsed.RefundReference,
                    EmployeeId = employee.Id,
                    HotelCode = parsed.HotelCode ?? employee.HotelCode,
                    ImportBatch = batch,
                    CreatedAt = DateTime.UtcNow
                });
                batch.ImportedRows++;
            }
            catch (RowParseException ex)
            {
                batch.ErrorRows++;
                batch.Errors.Add(new ImportError
                {
                    LineNumber = lineNumber,
                    RawLine = rawLine,
                    Reason = ex.Message,
                    IsDuplicate = false
                });
            }
        }

        batch.FinishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("{Source} import finished: {Imported} imported, {Duplicates} duplicates, {Errors} errors from {File}",
            source, batch.ImportedRows, batch.DuplicateRows, batch.ErrorRows, fileName);

        return new ImportResultDto(ToDto(batch), batch.Errors.OrderBy(e => e.LineNumber).Select(ToDto).ToList());
    }

    public async Task<IReadOnlyList<ImportBatchDto>> ListBatchesAsync(CancellationToken ct = default)
        => (await _db.ImportBatches.OrderByDescending(b => b.Id).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ImportErrorDto>> ListErrorsAsync(int batchId, CancellationToken ct = default)
    {
        if (!await _db.ImportBatches.AnyAsync(b => b.Id == batchId, ct))
            throw new NotFoundException($"Import batch bulunamadı: {batchId}");

        return (await _db.ImportErrors.Where(e => e.BatchId == batchId).OrderBy(e => e.LineNumber).ToListAsync(ct))
            .Select(ToDto).ToList();
    }

    private static string DedupKey(string documentNo, string productCode) => $"{documentNo}|{productCode}";

    private static ImportBatchDto ToDto(ImportBatch b)
        => new(b.Id, b.SourceSystem, b.FileName, b.StartedAt, b.FinishedAt, b.TotalRows, b.ImportedRows, b.DuplicateRows, b.ErrorRows, b.ImportedBy);

    private static ImportErrorDto ToDto(ImportError e)
        => new(e.Id, e.LineNumber, e.RawLine, e.Reason, e.IsDuplicate);
}
