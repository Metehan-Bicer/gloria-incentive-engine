using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Infrastructure;
using Gloria.Incentive.Api.Periods;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Calculation;

public class CommissionCalculationService
{
    private readonly AppDbContext _db;
    private readonly CommissionCalculator _calculator;
    private readonly PeriodService _periods;
    private readonly ICurrentUser _currentUser;

    public CommissionCalculationService(AppDbContext db, CommissionCalculator calculator, PeriodService periods, ICurrentUser currentUser)
    {
        _db = db;
        _calculator = calculator;
        _periods = periods;
        _currentUser = currentUser;
    }

    public async Task<CommissionResultDto> GetOrCalculateAsync(string employeeNo, int year, int month, CancellationToken ct = default)
    {
        PeriodService.ValidateYearMonth(year, month);

        var employee = await _db.Employees.Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo, ct)
            ?? throw new NotFoundException($"Personel bulunamadı: {employeeNo}");

        var period = await _periods.GetOrCreateAsync(year, month, ct);

        if (period.IsClosed)
        {
            var frozen = await LoadCalculationAsync(employee.Id, year, month, ct);
            if (frozen is null)
            {
                var empty = new CalculationResult();
                return await ToDtoAsync(employee, period, empty, null, ct);
            }
            return await ToDtoAsync(employee, period, frozen, ct);
        }

        var calculation = await CalculateAndStoreAsync(employee, year, month, ct);
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(employee, period, calculation, ct);
    }

    public async Task<PeriodSummaryDto> RunForAllAsync(int year, int month, CancellationToken ct = default)
    {
        var period = await _periods.GetOrCreateAsync(year, month, ct);
        if (period.IsClosed)
            throw new ConflictException($"{year}-{month:00} dönemi kapatılmış, yeniden hesaplama yapılamaz.");

        var employees = await _db.Employees.Include(e => e.Department).OrderBy(e => e.EmployeeNo).ToListAsync(ct);
        foreach (var employee in employees)
            await CalculateAndStoreAsync(employee, year, month, ct);

        await _db.SaveChangesAsync(ct);
        return await GetPeriodSummaryAsync(year, month, ct);
    }

    public async Task<PeriodSummaryDto> GetPeriodSummaryAsync(int year, int month, CancellationToken ct = default)
    {
        PeriodService.ValidateYearMonth(year, month);
        var period = await _periods.FindAsync(year, month, ct);

        var employees = await _db.Employees.Include(e => e.Department).OrderBy(e => e.EmployeeNo).ToListAsync(ct);
        var calculations = await _db.CommissionCalculations
            .Where(c => c.Year == year && c.Month == month)
            .ToDictionaryAsync(c => c.EmployeeId, ct);

        var rows = employees.Select(e =>
        {
            calculations.TryGetValue(e.Id, out var calc);
            return new EmployeeCommissionSummaryDto(
                e.EmployeeNo, e.FullName, e.Department.Name, e.HotelCode,
                calc?.GrossSales ?? 0, calc?.RefundTotal ?? 0, calc?.TotalCommission ?? 0,
                calc?.CalculatedAt, calc?.IsFinal ?? false);
        }).ToList();

        return new PeriodSummaryDto(
            year, month,
            period?.Status ?? PeriodStatus.Open,
            period?.ClosedAt, period?.ClosedBy,
            rows.Count(r => r.CalculatedAt is not null),
            rows.Sum(r => r.TotalCommission),
            rows);
    }

    private async Task<CommissionCalculation> CalculateAndStoreAsync(Employee employee, int year, int month, CancellationToken ct)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);

        var sales = await _db.SaleRecords
            .Include(s => s.Employee)
            .Where(s => s.EmployeeId == employee.Id && s.TransactionDate >= start && s.TransactionDate < end)
            .ToListAsync(ct);

        var rules = await _db.CommissionRules
            .Where(r => r.IsActive && r.ValidFrom < end && (r.ValidTo == null || r.ValidTo >= start))
            .ToListAsync(ct);

        var result = _calculator.Calculate(employee, sales, rules, year, month);

        var existing = await LoadCalculationAsync(employee.Id, year, month, ct);
        if (existing is not null)
            _db.CommissionCalculations.Remove(existing);

        var calculation = new CommissionCalculation
        {
            EmployeeId = employee.Id,
            Employee = employee,
            Year = year,
            Month = month,
            GrossSales = result.GrossSales,
            RefundTotal = result.RefundTotal,
            TotalCommission = result.TotalCommission,
            CalculatedAt = DateTime.UtcNow,
            CalculatedBy = _currentUser.DisplayName,
            IsFinal = false,
            Lines = result.Lines.Select(l => new CommissionCalculationLine
            {
                Sequence = l.Sequence,
                LineType = l.LineType,
                SaleRecordId = l.SaleRecordId,
                RuleId = l.RuleId,
                RuleName = l.RuleName,
                BaseAmount = l.BaseAmount,
                Rate = l.Rate,
                Amount = l.Amount,
                Description = l.Description
            }).ToList()
        };

        _db.CommissionCalculations.Add(calculation);
        return calculation;
    }

    private Task<CommissionCalculation?> LoadCalculationAsync(int employeeId, int year, int month, CancellationToken ct)
        => _db.CommissionCalculations
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.Year == year && c.Month == month, ct);

    private async Task<CommissionResultDto> ToDtoAsync(Employee employee, Period period, CommissionCalculation calculation, CancellationToken ct)
    {
        var result = new CalculationResult
        {
            GrossSales = calculation.GrossSales,
            RefundTotal = calculation.RefundTotal,
            TotalCommission = calculation.TotalCommission
        };
        result.Lines.AddRange(calculation.Lines.OrderBy(l => l.Sequence).Select(l => new CalculationLine
        {
            Sequence = l.Sequence,
            LineType = l.LineType,
            SaleRecordId = l.SaleRecordId,
            RuleId = l.RuleId,
            RuleName = l.RuleName,
            BaseAmount = l.BaseAmount,
            Rate = l.Rate,
            Amount = l.Amount,
            Description = l.Description
        }));

        return await ToDtoAsync(employee, period, result, calculation, ct);
    }

    private async Task<CommissionResultDto> ToDtoAsync(Employee employee, Period period, CalculationResult result, CommissionCalculation? calculation, CancellationToken ct)
    {
        var saleIds = result.Lines.Where(l => l.SaleRecordId.HasValue).Select(l => l.SaleRecordId!.Value).Distinct().ToList();
        var sales = await _db.SaleRecords.Where(s => saleIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        var ruleTypes = await _db.CommissionRules.ToDictionaryAsync(r => r.Id, r => r.RuleType, ct);

        var lines = result.Lines.Select(l =>
        {
            SaleInfoDto? sale = null;
            if (l.SaleRecordId is int id && sales.TryGetValue(id, out var s))
                sale = new SaleInfoDto(s.SourceSystem, s.ExternalDocumentNo, s.TransactionDate, s.ProductCode, s.ProductName, s.ProductCategory, s.Amount, s.IsRefund);

            return new CalculationLineDto(l.Sequence, l.LineType, l.SaleRecordId, l.RuleId, l.RuleName, l.BaseAmount, l.Rate, l.Amount, l.Description, sale);
        }).ToList();

        var summaries = result.Lines
            .Where(l => l.LineType == CalculationLineType.RuleSubtotal && l.RuleId.HasValue)
            .Select(l => new RuleSummaryDto(
                l.RuleId!.Value,
                l.RuleName ?? string.Empty,
                ruleTypes.TryGetValue(l.RuleId.Value, out var t) ? t : default,
                result.Lines.Count(x => x.RuleId == l.RuleId && x.SaleRecordId.HasValue),
                l.BaseAmount,
                l.Amount))
            .ToList();

        return new CommissionResultDto(
            calculation?.Id ?? 0,
            employee.EmployeeNo,
            employee.FullName,
            employee.Department.Name,
            employee.HotelCode,
            period.Year,
            period.Month,
            result.GrossSales,
            result.RefundTotal,
            result.GrossSales - result.RefundTotal,
            result.TotalCommission,
            calculation?.CalculatedAt ?? DateTime.UtcNow,
            calculation?.CalculatedBy ?? _currentUser.DisplayName,
            calculation?.IsFinal ?? false,
            period.IsClosed,
            summaries,
            lines);
    }
}
