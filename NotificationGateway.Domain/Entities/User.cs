namespace NotificationGateway.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Role { get; private set; } = "User"; // Admin, Manager, User
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ApplicationClient Application { get; private set; } = null!;
    public ICollection<NotificationTemplate> Templates { get; private set; } = new List<NotificationTemplate>();

    private User() { }

    public static User Create(
        Guid tenantId,
        Guid applicationId,
        string email,
        string passwordHash,
        string fullName,
        string role = "User")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
