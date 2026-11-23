using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class ProviderDefinition
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public bool IsActive { get; private set; }
    public string? Description { get; private set; }

    private ProviderDefinition() { }

    public static ProviderDefinition Create(string code, string displayName, NotificationChannel channel, string? description = null)
    {
        return new ProviderDefinition
        {
            Id = Guid.NewGuid(),
            Code = code.ToUpperInvariant(),
            DisplayName = displayName,
            Channel = channel,
            IsActive = true,
            Description = description
        };
    }
}
