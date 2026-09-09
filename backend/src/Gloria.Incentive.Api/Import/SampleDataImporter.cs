using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Import;

public class SampleDataImporter
{
    private static readonly (SourceSystem Source, string File)[] SampleFiles =
    [
        (SourceSystem.PMS, "pms_fidelio_2026_08.csv"),
        (SourceSystem.POS, "pos_flyby_2026_08.csv"),
        (SourceSystem.ERP, "erp_jde_2026_08.csv")
    ];

    private readonly AppDbContext _db;
    private readonly CsvImportService _import;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SampleDataImporter> _logger;

    public SampleDataImporter(AppDbContext db, CsvImportService import, IConfiguration configuration, ILogger<SampleDataImporter> logger)
    {
        _db = db;
        _import = import;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ImportIfEmptyAsync(CancellationToken ct = default)
    {
        if (!_configuration.GetValue<bool>("Import:AutoImportSamples")) return;
        if (await _db.SaleRecords.AnyAsync(ct)) return;

        var dataDirectory = _configuration["DataDirectory"] ?? "data";

        foreach (var (source, file) in SampleFiles)
        {
            var path = Path.Combine(dataDirectory, file);
            if (!File.Exists(path))
            {
                _logger.LogWarning("Sample file not found: {Path}", path);
                continue;
            }

            await using var stream = File.OpenRead(path);
            await _import.ImportAsync(source, file, stream, ct);
        }
    }
}
