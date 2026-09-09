using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Calculation;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Periods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Controllers;

[ApiController]
[Route("api/periods")]
[Authorize(Roles = Roles.AdminOrAccounting)]
public class PeriodsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PeriodService _periods;
    private readonly CommissionCalculationService _calculations;
    private readonly ICurrentUser _currentUser;

    public PeriodsController(AppDbContext db, PeriodService periods, CommissionCalculationService calculations, ICurrentUser currentUser)
    {
        _db = db;
        _periods = periods;
        _calculations = calculations;
        _currentUser = currentUser;
    }

    public record PeriodDto(int Year, int Month, PeriodStatus Status, DateTime? ClosedAt, string? ClosedBy, int SaleCount, int CalculatedEmployees, decimal TotalCommission);

    [HttpGet]
    public async Task<IReadOnlyList<PeriodDto>> List(CancellationToken ct)
    {
        var periods = await _db.Periods.ToListAsync(ct);

        var saleMonths = await _db.SaleRecords
            .GroupBy(s => new { s.TransactionDate.Year, s.TransactionDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);

        var calcMonths = await _db.CommissionCalculations
            .GroupBy(c => new { c.Year, c.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Total = g.Sum(c => c.TotalCommission) })
            .ToListAsync(ct);

        var keys = periods.Select(p => (p.Year, p.Month))
            .Concat(saleMonths.Select(s => (s.Year, s.Month)))
            .Concat(calcMonths.Select(c => (c.Year, c.Month)))
            .Distinct()
            .OrderByDescending(k => k.Year).ThenByDescending(k => k.Month);

        return keys.Select(k =>
        {
            var period = periods.FirstOrDefault(p => p.Year == k.Year && p.Month == k.Month);
            var sales = saleMonths.FirstOrDefault(s => s.Year == k.Year && s.Month == k.Month);
            var calc = calcMonths.FirstOrDefault(c => c.Year == k.Year && c.Month == k.Month);
            return new PeriodDto(k.Year, k.Month, period?.Status ?? PeriodStatus.Open, period?.ClosedAt, period?.ClosedBy,
                sales?.Count ?? 0, calc?.Count ?? 0, calc?.Total ?? 0);
        }).ToList();
    }

    [HttpPost("{year:int}/{month:int}/close")]
    public async Task<PeriodSummaryDto> Close(int year, int month, CancellationToken ct)
    {
        await _calculations.RunForAllAsync(year, month, ct);
        await _periods.CloseAsync(year, month, _currentUser.DisplayName, ct);
        return await _calculations.GetPeriodSummaryAsync(year, month, ct);
    }
}
