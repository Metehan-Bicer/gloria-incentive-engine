using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Import;
using Gloria.Incentive.Api.Infrastructure;
using Gloria.Incentive.Api.Periods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize(Roles = Roles.AdminOrAccounting)]
public class SalesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PeriodService _periods;

    public SalesController(AppDbContext db, PeriodService periods)
    {
        _db = db;
        _periods = periods;
    }

    public record SaleDto(
        int Id,
        SourceSystem SourceSystem,
        string ExternalDocumentNo,
        DateOnly TransactionDate,
        string ProductCode,
        string ProductName,
        string ProductCategory,
        int Quantity,
        decimal Amount,
        string Currency,
        bool IsRefund,
        string? RefundReference,
        string EmployeeNo,
        string HotelCode,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public record SaleUpdateInput(DateOnly TransactionDate, string ProductCode, string ProductName, int Quantity, decimal Amount, bool IsRefund);

    [HttpGet]
    public async Task<IReadOnlyList<SaleDto>> List([FromQuery] int year, [FromQuery] int month, [FromQuery] string? employeeNo, CancellationToken ct)
    {
        PeriodService.ValidateYearMonth(year, month);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);

        var query = _db.SaleRecords.Include(s => s.Employee)
            .Where(s => s.TransactionDate >= start && s.TransactionDate < end);

        if (!string.IsNullOrWhiteSpace(employeeNo))
            query = query.Where(s => s.Employee.EmployeeNo == employeeNo);

        return await query
            .OrderBy(s => s.TransactionDate).ThenBy(s => s.Id)
            .Select(s => ToDto(s))
            .ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<SaleDto> Get(int id, CancellationToken ct)
        => ToDto(await FindAsync(id, ct));

    [HttpPut("{id:int}")]
    public async Task<SaleDto> Update(int id, SaleUpdateInput input, CancellationToken ct)
    {
        var sale = await FindAsync(id, ct);

        await _periods.EnsureOpenAsync(sale.TransactionDate, ct);
        await _periods.EnsureOpenAsync(input.TransactionDate, ct);

        if (input.Amount <= 0) throw new BadRequestException("Tutar sıfırdan büyük olmalıdır.");
        if (input.Quantity <= 0) throw new BadRequestException("Adet sıfırdan büyük olmalıdır.");
        if (string.IsNullOrWhiteSpace(input.ProductCode)) throw new BadRequestException("Ürün kodu zorunludur.");

        sale.TransactionDate = input.TransactionDate;
        sale.ProductCode = input.ProductCode.Trim().ToUpperInvariant();
        sale.ProductName = string.IsNullOrWhiteSpace(input.ProductName) ? sale.ProductName : input.ProductName.Trim();
        sale.ProductCategory = ProductCatalog.CategoryFor(sale.ProductCode);
        sale.Quantity = input.Quantity;
        sale.Amount = input.Amount;
        sale.IsRefund = input.IsRefund;
        sale.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(sale);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var sale = await FindAsync(id, ct);
        await _periods.EnsureOpenAsync(sale.TransactionDate, ct);

        _db.SaleRecords.Remove(sale);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<SaleRecord> FindAsync(int id, CancellationToken ct)
        => await _db.SaleRecords.Include(s => s.Employee).FirstOrDefaultAsync(s => s.Id == id, ct)
           ?? throw new NotFoundException($"Satış kaydı bulunamadı: {id}");

    private static SaleDto ToDto(SaleRecord s)
        => new(s.Id, s.SourceSystem, s.ExternalDocumentNo, s.TransactionDate, s.ProductCode, s.ProductName, s.ProductCategory,
            s.Quantity, s.Amount, s.Currency, s.IsRefund, s.RefundReference, s.Employee.EmployeeNo, s.HotelCode, s.CreatedAt, s.UpdatedAt);
}
