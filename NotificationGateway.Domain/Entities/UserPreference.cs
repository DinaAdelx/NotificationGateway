using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class UserPreference
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string ExternalUserId { get; private set; } = string.Empty;
    public string? PreferredLanguage { get; private set; }
    public IList<NotificationChannel> PreferredChannels { get; private set; } = new List<NotificationChannel>();
    public IList<string> DisabledNotificationTypes { get; private set; } = new List<string>();
    public bool QuietHoursEnabled { get; private set; }
    public TimeOnly? QuietHoursStart { get; private set; }
    public TimeOnly? QuietHoursEnd { get; private set; }
    public string? QuietHoursTimezone { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private UserPreference() { }

    public static UserPreference Create(Guid tenantId, Guid applicationId, string externalUserId)
    {
        return new UserPreference
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            ExternalUserId = externalUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetQuietHours(TimeOnly start, TimeOnly end, string timezone)
    {
        QuietHoursEnabled = true;
        QuietHoursStart = start;
        QuietHoursEnd = end;
        QuietHoursTimezone = timezone;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DisableQuietHours() { QuietHoursEnabled = false; UpdatedAt = DateTime.UtcNow; }
}
