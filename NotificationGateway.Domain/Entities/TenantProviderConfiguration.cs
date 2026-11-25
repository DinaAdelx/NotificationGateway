using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class TenantProviderConfiguration
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid? UserId { get; private set; }
    public string ProviderCode { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsPrimary { get; private set; }
    public int Priority { get; private set; }
    /// <summary>Non-secret provider settings (JSON).</summary>
    public string ConfigurationJson { get; private set; } = "{}";
    /// <summary>Encrypted credentials or a secret manager reference.</summary>
    public string? EncryptedCredentials { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private TenantProviderConfiguration() { }

    public static TenantProviderConfiguration Create(
        Guid tenantId, Guid applicationId, string providerCode,
        NotificationChannel channel, string configJson,
        string? encryptedCredentials = null, bool isPrimary = false, int priority = 0, Guid? userId = null)
    {
        return new TenantProviderConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserId = userId,
            ProviderCode = providerCode,
            Channel = channel,
            IsEnabled = true,
            IsPrimary = isPrimary,
            Priority = priority,
            ConfigurationJson = configJson,
            EncryptedCredentials = encryptedCredentials,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Disable()  { IsEnabled = false; UpdatedAt = DateTime.UtcNow; }
    public void Enable()   { IsEnabled = true;  UpdatedAt = DateTime.UtcNow; }
    public void UpdateCredentials(string encrypted) { EncryptedCredentials = encrypted; UpdatedAt = DateTime.UtcNow; }

    public void UpdateConfiguration(string configJson, bool isPrimary = true, int priority = 1)
    {
        ConfigurationJson = configJson;
        IsPrimary = isPrimary;
        Priority = priority;
        IsEnabled = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
