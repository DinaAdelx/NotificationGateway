using Microsoft.EntityFrameworkCore;
using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Infrastructure.Persistence;

public class NotificationGatewayDbContext(DbContextOptions<NotificationGatewayDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApplicationClient> ApplicationClients => Set<ApplicationClient>();
    public DbSet<User> Users => Set<User>();
    public DbSet<NotificationTypeDefinition> NotificationTypeDefinitions => Set<NotificationTypeDefinition>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<TenantProviderConfiguration> TenantProviderConfigurations => Set<TenantProviderConfiguration>();
    public DbSet<ProviderDefinition> ProviderDefinitions => Set<ProviderDefinition>();
    public DbSet<RoutingRule> RoutingRules => Set<RoutingRule>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<NotificationOutbox> NotificationOutbox => Set<NotificationOutbox>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationGatewayDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
