using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class RoutingRule
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid? NotificationTypeDefinitionId { get; private set; }
    public NotificationPriority? Priority { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string PrimaryProviderCode { get; private set; } = string.Empty;
    public IList<string> FallbackProviderCodes { get; private set; } = new List<string>();
    public string FallbackPolicy { get; private set; } = "TransientFailureOnly";
    public bool IsActive { get; private set; }
    public int Order { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private RoutingRule() { }

    public static RoutingRule Create(
        Guid tenantId, Guid applicationId, NotificationChannel channel,
        string primaryProviderCode, IEnumerable<string>? fallbacks = null,
        Guid? notificationTypeId = null, NotificationPriority? priority = null,
        string fallbackPolicy = "TransientFailureOnly", int order = 0)
    {
        return new RoutingRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Channel = channel,
            PrimaryProviderCode = primaryProviderCode,
            FallbackProviderCodes = fallbacks?.ToList() ?? new List<string>(),
            NotificationTypeDefinitionId = notificationTypeId,
            Priority = priority,
            FallbackPolicy = fallbackPolicy,
            IsActive = true,
            Order = order,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate()   { IsActive = true;  UpdatedAt = DateTime.UtcNow; }
}
