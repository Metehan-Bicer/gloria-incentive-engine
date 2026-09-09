using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = Roles.AdminOrAccounting)]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    public record AuditLogDto(long Id, string EntityName, string EntityId, AuditAction Action, string? OldValuesJson, string? NewValuesJson, string ChangedBy, DateTime ChangedAt);

    [HttpGet]
    public async Task<IReadOnlyList<AuditLogDto>> List([FromQuery] string? entity, [FromQuery] string? entityId, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(a => a.EntityName == entity);
        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(a => a.EntityId == entityId);

        return await query
            .OrderByDescending(a => a.Id)
            .Take(Math.Clamp(take, 1, 500))
            .Select(a => new AuditLogDto(a.Id, a.EntityName, a.EntityId, a.Action, a.OldValuesJson, a.NewValuesJson, a.ChangedBy, a.ChangedAt))
            .ToListAsync(ct);
    }
}
