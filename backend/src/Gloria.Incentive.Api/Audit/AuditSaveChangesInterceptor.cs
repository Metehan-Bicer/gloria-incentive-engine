using System.Text.Json;
using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gloria.Incentive.Api.Audit;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<Type> AuditedTypes = [typeof(CommissionRule), typeof(SaleRecord), typeof(Period)];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly ICurrentUser _currentUser;
    private readonly List<PendingAudit> _pending = new();
    private IDbContextTransaction? _ownedTransaction;

    public AuditSaveChangesInterceptor(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null && Collect(eventData.Context) && eventData.Context.Database.CurrentTransaction is null)
            _ownedTransaction = eventData.Context.Database.BeginTransaction();

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && Collect(eventData.Context) && eventData.Context.Database.CurrentTransaction is null)
            _ownedTransaction = await eventData.Context.Database.BeginTransactionAsync(cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
        {
            Flush(eventData.Context);
            if (_ownedTransaction is not null)
            {
                _ownedTransaction.Commit();
                _ownedTransaction.Dispose();
                _ownedTransaction = null;
            }
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await FlushAsync(eventData.Context, cancellationToken);
            if (_ownedTransaction is not null)
            {
                await _ownedTransaction.CommitAsync(cancellationToken);
                await _ownedTransaction.DisposeAsync();
                _ownedTransaction = null;
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Abort();
        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Abort();
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Abort()
    {
        _pending.Clear();
        if (_ownedTransaction is null) return;
        _ownedTransaction.Rollback();
        _ownedTransaction.Dispose();
        _ownedTransaction = null;
    }

    private bool Collect(DbContext context)
    {
        var before = _pending.Count;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (!AuditedTypes.Contains(entry.Metadata.ClrType)) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var audit = new PendingAudit
            {
                Entry = entry,
                EntityName = entry.Metadata.ClrType.Name,
                Action = entry.State switch
                {
                    EntityState.Added => AuditAction.Create,
                    EntityState.Deleted => AuditAction.Delete,
                    _ => AuditAction.Update
                }
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    break;
                case EntityState.Deleted:
                    audit.OldValues = Snapshot(entry, useOriginal: true);
                    audit.EntityId = KeyOf(entry);
                    break;
                case EntityState.Modified:
                    var changed = entry.Properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)).ToList();
                    if (changed.Count == 0) continue;
                    audit.OldValues = changed.ToDictionary(p => Camel(p.Metadata.Name), p => p.OriginalValue);
                    audit.NewValues = changed.ToDictionary(p => Camel(p.Metadata.Name), p => p.CurrentValue);
                    audit.EntityId = KeyOf(entry);
                    break;
            }

            _pending.Add(audit);
        }

        return _pending.Count > before;
    }

    private void Flush(DbContext context)
    {
        if (!Materialize(context)) return;
        context.SaveChanges();
    }

    private async Task FlushAsync(DbContext context, CancellationToken ct)
    {
        if (!Materialize(context)) return;
        await context.SaveChangesAsync(ct);
    }

    private bool Materialize(DbContext context)
    {
        if (_pending.Count == 0) return false;

        var now = DateTime.UtcNow;
        var actor = _currentUser.DisplayName;

        foreach (var audit in _pending)
        {
            if (audit.Action == AuditAction.Create)
                audit.NewValues = Snapshot(audit.Entry, useOriginal: false);

            context.Set<AuditLog>().Add(new AuditLog
            {
                EntityName = audit.EntityName,
                EntityId = audit.EntityId ?? KeyOf(audit.Entry),
                Action = audit.Action,
                OldValuesJson = audit.OldValues is null ? null : JsonSerializer.Serialize(audit.OldValues, JsonOptions),
                NewValuesJson = audit.NewValues is null ? null : JsonSerializer.Serialize(audit.NewValues, JsonOptions),
                ChangedBy = actor,
                ChangedAt = now
            });
        }

        _pending.Clear();
        return true;
    }

    private static Dictionary<string, object?> Snapshot(EntityEntry entry, bool useOriginal)
        => entry.Properties
            .Where(p => !p.Metadata.IsShadowProperty())
            .ToDictionary(p => Camel(p.Metadata.Name), p => useOriginal ? p.OriginalValue : p.CurrentValue);

    private static string KeyOf(EntityEntry entry)
        => string.Join("/", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString()));

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private class PendingAudit
    {
        public required EntityEntry Entry { get; init; }
        public required string EntityName { get; init; }
        public required AuditAction Action { get; init; }
        public string? EntityId { get; set; }
        public Dictionary<string, object?>? OldValues { get; set; }
        public Dictionary<string, object?>? NewValues { get; set; }
    }
}
