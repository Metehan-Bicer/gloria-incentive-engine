using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Periods;

public class PeriodService
{
    private readonly AppDbContext _db;

    public PeriodService(AppDbContext db)
    {
        _db = db;
    }

    public static void ValidateYearMonth(int year, int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            throw new BadRequestException("Geçersiz dönem. Yıl 2000-2100, ay 1-12 aralığında olmalıdır.");
    }

    public Task<Period?> FindAsync(int year, int month, CancellationToken ct = default)
        => _db.Periods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month, ct);

    public async Task<Period> GetOrCreateAsync(int year, int month, CancellationToken ct = default)
    {
        ValidateYearMonth(year, month);
        var period = await FindAsync(year, month, ct);
        if (period is not null) return period;

        period = new Period { Year = year, Month = month, Status = PeriodStatus.Open };
        _db.Periods.Add(period);
        await _db.SaveChangesAsync(ct);
        return period;
    }

    public async Task<bool> IsClosedAsync(int year, int month, CancellationToken ct = default)
    {
        var period = await FindAsync(year, month, ct);
        return period?.IsClosed == true;
    }

    public async Task<Period> CloseAsync(int year, int month, string closedBy, CancellationToken ct = default)
    {
        var period = await GetOrCreateAsync(year, month, ct);
        if (period.IsClosed)
            throw new ConflictException($"{year}-{month:00} dönemi zaten kapatılmış.");

        var calculations = await _db.CommissionCalculations
            .Where(c => c.Year == year && c.Month == month)
            .ToListAsync(ct);

        foreach (var calculation in calculations)
            calculation.IsFinal = true;

        period.Status = PeriodStatus.Closed;
        period.ClosedAt = DateTime.UtcNow;
        period.ClosedBy = closedBy;

        await _db.SaveChangesAsync(ct);
        return period;
    }

    public async Task EnsureOpenAsync(DateOnly date, CancellationToken ct = default)
    {
        if (await IsClosedAsync(date.Year, date.Month, ct))
            throw new ConflictException($"{date.Year}-{date.Month:00} dönemi kapatılmış, kayıtlar değiştirilemez.");
    }
}
