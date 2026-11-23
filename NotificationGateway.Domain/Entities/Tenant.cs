namespace NotificationGateway.Domain.Entities;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<ApplicationClient> ApplicationClients { get; private set; } = new List<ApplicationClient>();

    private Tenant() { }

    public static Tenant Create(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate()   { IsActive = true;  UpdatedAt = DateTime.UtcNow; }
}
