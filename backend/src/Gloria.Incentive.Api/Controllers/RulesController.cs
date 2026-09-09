using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Import;
using Gloria.Incentive.Api.Infrastructure;
using Gloria.Incentive.Api.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Controllers;

[ApiController]
[Route("api/rules")]
[Authorize(Roles = Roles.AdminOrAccounting)]
public class RulesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RuleStrategyResolver _resolver;

    public RulesController(AppDbContext db, RuleStrategyResolver resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    public record RuleDto(
        int Id,
        string Name,
        RuleType RuleType,
        string ParametersJson,
        string? ProductCategory,
        string? ProductCode,
        SourceSystem? SourceSystem,
        int? DepartmentId,
        string? DepartmentName,
        int Priority,
        DateOnly ValidFrom,
        DateOnly? ValidTo,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public record RuleInput(
        string Name,
        RuleType RuleType,
        string ParametersJson,
        string? ProductCategory,
        string? ProductCode,
        SourceSystem? SourceSystem,
        int? DepartmentId,
        int Priority,
        DateOnly ValidFrom,
        DateOnly? ValidTo,
        bool IsActive);

    public record RuleTypeDto(RuleType Type, string Label, string ParameterHint);

    public record RuleOptionsDto(IReadOnlyList<RuleTypeDto> Types, IReadOnlyList<string> Categories, IReadOnlyList<DepartmentDto> Departments, IReadOnlyList<string> Sources);

    public record DepartmentDto(int Id, string Name);

    [HttpGet]
    public async Task<IReadOnlyList<RuleDto>> List(CancellationToken ct)
        => await _db.CommissionRules.Include(r => r.Department)
            .OrderBy(r => r.Priority).ThenBy(r => r.Id)
            .Select(r => ToDto(r))
            .ToListAsync(ct);

    [HttpGet("options")]
    public async Task<RuleOptionsDto> Options(CancellationToken ct)
    {
        var departments = await _db.Departments.OrderBy(d => d.Name).Select(d => new DepartmentDto(d.Id, d.Name)).ToListAsync(ct);
        return new RuleOptionsDto(
            _resolver.All.Select(s => new RuleTypeDto(s.Type, s.Label, s.ParameterHint)).ToList(),
            ProductCatalog.KnownCategories.ToList(),
            departments,
            Enum.GetNames<SourceSystem>());
    }

    [HttpGet("{id:int}")]
    public async Task<RuleDto> Get(int id, CancellationToken ct)
    {
        var rule = await _db.CommissionRules.Include(r => r.Department).FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw new NotFoundException($"Kural bulunamadı: {id}");
        return ToDto(rule);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<RuleDto>> Create(RuleInput input, CancellationToken ct)
    {
        await ValidateAsync(input, ct);

        var rule = new CommissionRule { CreatedAt = DateTime.UtcNow };
        Apply(rule, input);
        _db.CommissionRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(rule).Reference(r => r.Department).LoadAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, ToDto(rule));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<RuleDto> Update(int id, RuleInput input, CancellationToken ct)
    {
        var rule = await _db.CommissionRules.FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw new NotFoundException($"Kural bulunamadı: {id}");

        await ValidateAsync(input, ct);
        Apply(rule, input);
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _db.Entry(rule).Reference(r => r.Department).LoadAsync(ct);
        return ToDto(rule);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var rule = await _db.CommissionRules.FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw new NotFoundException($"Kural bulunamadı: {id}");

        var used = await _db.CommissionCalculationLines.AnyAsync(l => l.RuleId == id, ct);
        if (used)
            throw new ConflictException("Bu kural hesaplamalarda kullanılmış. Silmek yerine pasife alın ya da bitiş tarihi verin.");

        _db.CommissionRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ValidateAsync(RuleInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new BadRequestException("Kural adı zorunludur.");

        if (input.ValidTo is not null && input.ValidTo < input.ValidFrom)
            throw new BadRequestException("Bitiş tarihi başlangıç tarihinden önce olamaz.");

        if (input.DepartmentId is int departmentId && !await _db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            throw new BadRequestException($"Departman bulunamadı: {departmentId}");

        ICommissionRuleStrategy strategy;
        try
        {
            strategy = _resolver.Resolve(input.RuleType);
        }
        catch (InvalidOperationException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        strategy.ValidateParameters(input.ParametersJson);
    }

    private static void Apply(CommissionRule rule, RuleInput input)
    {
        rule.Name = input.Name.Trim();
        rule.RuleType = input.RuleType;
        rule.ParametersJson = input.ParametersJson.Trim();
        rule.ProductCategory = Normalize(input.ProductCategory)?.ToUpperInvariant();
        rule.ProductCode = Normalize(input.ProductCode)?.ToUpperInvariant();
        rule.SourceSystem = input.SourceSystem;
        rule.DepartmentId = input.DepartmentId;
        rule.Priority = input.Priority;
        rule.ValidFrom = input.ValidFrom;
        rule.ValidTo = input.ValidTo;
        rule.IsActive = input.IsActive;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RuleDto ToDto(CommissionRule r)
        => new(r.Id, r.Name, r.RuleType, r.ParametersJson, r.ProductCategory, r.ProductCode, r.SourceSystem,
            r.DepartmentId, r.Department == null ? null : r.Department.Name, r.Priority, r.ValidFrom, r.ValidTo, r.IsActive, r.CreatedAt, r.UpdatedAt);
}
