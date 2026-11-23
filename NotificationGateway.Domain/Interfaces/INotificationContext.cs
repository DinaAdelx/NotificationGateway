using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Interfaces;

/// <summary>Caller-resolved tenant/application context from authenticated credentials.</summary>
public interface INotificationContext
{
    Guid TenantId { get; }
    Guid ApplicationId { get; }
    string ClientId { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsSystemAdministrator { get; }
}
