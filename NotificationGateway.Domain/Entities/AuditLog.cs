namespace NotificationGateway.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string? ActorClientId { get; private set; }
    public string? ChangesJson { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        Guid tenantId, Guid? applicationId, string action,
        string entityType, Guid? entityId = null,
        string? actorClientId = null, string? changesJson = null)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorClientId = actorClientId,
            ChangesJson = changesJson,
            CreatedAt = DateTime.UtcNow
        };
    }
}
