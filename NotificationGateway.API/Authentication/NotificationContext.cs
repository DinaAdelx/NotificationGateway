using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.API.Authentication;

public class NotificationContext : INotificationContext
{
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
    public bool IsSystemAdministrator { get; set; }
}
