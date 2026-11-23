using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class NotificationTemplate
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid NotificationTypeDefinitionId { get; private set; }
    public Guid? UserId { get; private set; } // Associated user who owns/created this template
    public NotificationChannel Channel { get; private set; }
    public string Language { get; private set; } = "en";
    public int Version { get; private set; } = 1;
    public string? Subject { get; private set; }
    /// <summary>Channel-specific content as JSON.</summary>
    public string ContentJson { get; private set; } = "{}";
    public IList<string> Variables { get; private set; } = new List<string>();
    public TemplateStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public NotificationTypeDefinition NotificationType { get; private set; } = null!;
    public User? User { get; private set; }

    private NotificationTemplate() { }

    public static NotificationTemplate Create(
        Guid tenantId,
        Guid applicationId,
        Guid notificationTypeId,
        NotificationChannel channel,
        string language,
        string contentJson,
        string? subject = null,
        IEnumerable<string>? variables = null,
        Guid? userId = null)
    {
        return new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            NotificationTypeDefinitionId = notificationTypeId,
            UserId = userId,
            Channel = channel,
            Language = language,
            Version = 1,
            Subject = subject,
            ContentJson = contentJson,
            Variables = variables?.ToList() ?? new List<string>(),
            Status = TemplateStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Publish()
    {
        Status = TemplateStatus.Active;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = TemplateStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string contentJson, string? subject, IEnumerable<string>? variables)
    {
        ContentJson = contentJson;
        Subject = subject;
        Variables = variables?.ToList() ?? new List<string>();
        Status = TemplateStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }
}
