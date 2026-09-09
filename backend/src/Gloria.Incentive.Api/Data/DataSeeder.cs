using System.Globalization;
using Gloria.Incentive.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Data;

public class DataSeeder
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(AppDbContext db, IConfiguration configuration, ILogger<DataSeeder> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedEmployeesAsync(ct);
        await SeedRulesAsync(ct);
    }

    private async Task SeedEmployeesAsync(CancellationToken ct)
    {
        if (await _db.Employees.AnyAsync(ct)) return;

        var dataDirectory = _configuration["DataDirectory"] ?? "data";
        var path = Path.Combine(dataDirectory, "personel.csv");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Employee seed file not found at {Path}", path);
            return;
        }

        var departments = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
        var lines = await File.ReadAllLinesAsync(path, ct);
        var count = 0;

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(';');
            if (parts.Length < 5) continue;

            var departmentName = parts[2].Trim();
            if (!departments.TryGetValue(departmentName, out var department))
            {
                department = new Department { Name = departmentName };
                departments[departmentName] = department;
                _db.Departments.Add(department);
            }

            _db.Employees.Add(new Employee
            {
                EmployeeNo = parts[0].Trim(),
                FullName = parts[1].Trim(),
                Department = department,
                HotelCode = parts[3].Trim(),
                HireDate = DateOnly.ParseExact(parts[4].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                TerminationDate = parts.Length > 5 && !string.IsNullOrWhiteSpace(parts[5])
                    ? DateOnly.ParseExact(parts[5].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null
            });
            count++;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} employees from {Path}", count, path);
    }

    private async Task SeedRulesAsync(CancellationToken ct)
    {
        if (await _db.CommissionRules.AnyAsync(ct)) return;

        var validFrom = new DateOnly(2026, 1, 1);
        var now = DateTime.UtcNow;

        _db.CommissionRules.AddRange(
            new CommissionRule
            {
                Name = "SPA hizmet satışı %5",
                RuleType = RuleType.FixedPercentage,
                ParametersJson = """{"percentage":5}""",
                ProductCategory = "SPA",
                Priority = 10,
                ValidFrom = validFrom,
                CreatedAt = now
            },
            new CommissionRule
            {
                Name = "A la Carte rezervasyon kademeli prim",
                RuleType = RuleType.TieredRate,
                ParametersJson = """{"mode":"Marginal","tiers":[{"threshold":0,"rate":2},{"threshold":30000,"rate":4},{"threshold":60000,"rate":6}]}""",
                ProductCategory = "ALC",
                Priority = 20,
                ValidFrom = validFrom,
                CreatedAt = now
            },
            new CommissionRule
            {
                Name = "Buggy kiralama işlem başına 50 TL",
                RuleType = RuleType.FixedAmountPerTransaction,
                ParametersJson = """{"amount":50}""",
                ProductCategory = "BUGGY",
                Priority = 30,
                ValidFrom = validFrom,
                CreatedAt = now
            },
            new CommissionRule
            {
                Name = "Pavillon kullanımı %3",
                RuleType = RuleType.FixedPercentage,
                ParametersJson = """{"percentage":3}""",
                ProductCategory = "PAVILLON",
                Priority = 40,
                ValidFrom = validFrom,
                CreatedAt = now
            },
            new CommissionRule
            {
                Name = "SPA ürün satışı (POS) %4",
                RuleType = RuleType.FixedPercentage,
                ParametersJson = """{"percentage":4}""",
                ProductCategory = "SPA_RETAIL",
                SourceSystem = SourceSystem.POS,
                Priority = 50,
                ValidFrom = validFrom,
                CreatedAt = now
            },
            new CommissionRule
            {
                Name = "Bar satışı işlem başına 25 TL",
                RuleType = RuleType.FixedAmountPerTransaction,
                ParametersJson = """{"amount":25}""",
                ProductCategory = "BAR",
                SourceSystem = SourceSystem.POS,
                Priority = 60,
                ValidFrom = validFrom,
                CreatedAt = now
            });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded default commission rules");
    }
}
