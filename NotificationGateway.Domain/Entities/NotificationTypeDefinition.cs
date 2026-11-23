using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class NotificationTypeDefinition
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    /// <summary>JSON Schema string (Draft-07)</summary>
    public string PayloadSchemaJson { get; private set; } = "{}";
    public int SchemaVersion { get; private set; } = 1;
    public IList<NotificationChannel> AllowedChannels { get; private set; } = new List<NotificationChannel>();
    public string DefaultLanguage { get; private set; } = "en";
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ApplicationClient Application { get; private set; } = null!;
    public ICollection<NotificationTemplate> Templates { get; private set; } = new List<NotificationTemplate>();

    private NotificationTypeDefinition() { }

    public static NotificationTypeDefinition Create(
        Guid tenantId, Guid applicationId, string code, string displayName,
        string payloadSchemaJson, IEnumerable<NotificationChannel> allowedChannels,
        string defaultLanguage = "en", string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new NotificationTypeDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = code.ToUpperInvariant(),
            DisplayName = displayName,
            Description = description,
            PayloadSchemaJson = payloadSchemaJson,
            AllowedChannels = allowedChannels.ToList(),
            DefaultLanguage = defaultLanguage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateSchema(string newSchemaJson)
    {
        PayloadSchemaJson = newSchemaJson;
        SchemaVersion++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate()   { IsActive = true;  UpdatedAt = DateTime.UtcNow; }
}
