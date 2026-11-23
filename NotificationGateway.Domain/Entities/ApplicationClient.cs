namespace NotificationGateway.Domain.Entities;

public class ApplicationClient
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    /// <summary>Hashed API key — never store plain-text.</summary>
    public string HashedApiKey { get; private set; } = string.Empty;
    public string ApiKeyPrefix { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ICollection<NotificationTypeDefinition> NotificationTypes { get; private set; } = new List<NotificationTypeDefinition>();
    public ICollection<NotificationTemplate> Templates { get; private set; } = new List<NotificationTemplate>();

    private ApplicationClient() { }

    public static ApplicationClient Create(Guid tenantId, string name, string clientId, string hashedApiKey, string prefix)
    {
        return new ApplicationClient
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            ClientId = clientId,
            HashedApiKey = hashedApiKey,
            ApiKeyPrefix = prefix,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Revoke()     { IsActive = false; RevokedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow; }
    public void Reactivate() { IsActive = true;  RevokedAt = null;            UpdatedAt = DateTime.UtcNow; }
}
