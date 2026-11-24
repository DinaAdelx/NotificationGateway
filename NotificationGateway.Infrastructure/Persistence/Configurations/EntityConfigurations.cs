using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Name).HasMaxLength(200).IsRequired();
        b.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(t => t.Slug).IsUnique();
        b.HasMany(t => t.ApplicationClients).WithOne(a => a.Tenant).HasForeignKey(a => a.TenantId);
    }
}

public class ApplicationClientConfiguration : IEntityTypeConfiguration<ApplicationClient>
{
    public void Configure(EntityTypeBuilder<ApplicationClient> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Name).HasMaxLength(200).IsRequired();
        b.Property(a => a.ClientId).HasMaxLength(100).IsRequired();
        b.Property(a => a.HashedApiKey).HasMaxLength(512).IsRequired();
        b.Property(a => a.ApiKeyPrefix).HasMaxLength(20).IsRequired();
        b.HasIndex(a => new { a.TenantId, a.ClientId }).IsUnique();
    }
}

public class NotificationTypeDefinitionConfiguration : IEntityTypeConfiguration<NotificationTypeDefinition>
{
    public void Configure(EntityTypeBuilder<NotificationTypeDefinition> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Code).HasMaxLength(100).IsRequired();
        b.Property(t => t.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(t => t.PayloadSchemaJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(t => t.DefaultLanguage).HasMaxLength(10).IsRequired();
        b.HasIndex(t => new { t.TenantId, t.ApplicationId, t.Code }).IsUnique();

        b.HasOne(t => t.Tenant)
         .WithMany()
         .HasForeignKey(t => t.TenantId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(t => t.Application)
         .WithMany(a => a.NotificationTypes)
         .HasForeignKey(t => t.ApplicationId)
         .OnDelete(DeleteBehavior.Restrict);

        b.Property(t => t.AllowedChannels)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<NotificationChannel>>(v, (JsonSerializerOptions?)null)!);
    }
}

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Language).HasMaxLength(10).IsRequired();
        b.Property(t => t.ContentJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(t => t.Subject).HasMaxLength(500);
        b.Property(t => t.Channel).HasConversion<string>().HasMaxLength(50);
        b.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);

        b.Property(t => t.Variables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.HasKey(n => n.Id);
        b.Property(n => n.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(n => n.NotificationTypeCode).HasMaxLength(100).IsRequired();
        b.Property(n => n.Priority).HasConversion<string>().HasMaxLength(20);
        b.Property(n => n.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(n => n.IdempotencyKey).HasMaxLength(256);
        b.Property(n => n.CorrelationId).HasMaxLength(256);
        b.HasIndex(n => new { n.TenantId, n.ApplicationId, n.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.HasIndex(n => new { n.TenantId, n.ApplicationId, n.CreatedAt });
        b.HasMany(n => n.Deliveries).WithOne(d => d.Notification).HasForeignKey(d => d.NotificationId);
    }
}

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> b)
    {
        b.HasKey(d => d.Id);
        b.Property(d => d.Channel).HasConversion<string>().HasMaxLength(50);
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(d => d.RecipientAddress).HasMaxLength(500).IsRequired();
        b.HasIndex(d => new { d.TenantId, d.NotificationId });
        b.HasIndex(d => new { d.Status, d.NextAttemptAt });
        b.HasMany(d => d.Attempts).WithOne(a => a.Delivery).HasForeignKey(a => a.DeliveryId);
    }
}

public class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.ProviderCode).HasMaxLength(100).IsRequired();
        b.Property(a => a.ProviderMessageId).HasMaxLength(500);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.FailureCategory).HasConversion<string>().HasMaxLength(50);
        b.Property(a => a.ErrorCode).HasMaxLength(100);
        b.Property(a => a.SafeErrorMessage).HasMaxLength(1000);
    }
}

public class RoutingRuleConfiguration : IEntityTypeConfiguration<RoutingRule>
{
    public void Configure(EntityTypeBuilder<RoutingRule> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Channel).HasConversion<string>().HasMaxLength(50);
        b.Property(r => r.Priority).HasConversion<string?>().HasMaxLength(20);
        b.Property(r => r.PrimaryProviderCode).HasMaxLength(100).IsRequired();
        b.Property(r => r.FallbackPolicy).HasMaxLength(50);
        b.HasIndex(r => new { r.TenantId, r.ApplicationId, r.Channel, r.IsActive });

        b.Property(r => r.FallbackProviderCodes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!);
    }
}

public class TenantProviderConfigurationConfiguration : IEntityTypeConfiguration<TenantProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantProviderConfiguration> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.ProviderCode).HasMaxLength(100).IsRequired();
        b.Property(c => c.Channel).HasConversion<string>().HasMaxLength(50);
        b.Property(c => c.ConfigurationJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(c => c.EncryptedCredentials).HasColumnType("nvarchar(max)");
        b.HasIndex(c => new { c.TenantId, c.ApplicationId, c.Channel });
    }
}

public class OutboxConfiguration : IEntityTypeConfiguration<NotificationOutbox>
{
    public void Configure(EntityTypeBuilder<NotificationOutbox> b)
    {
        b.HasKey(o => o.Id);
        b.Property(o => o.EventType).HasMaxLength(100).IsRequired();
        b.Property(o => o.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        b.HasIndex(o => new { o.IsProcessed, o.CreatedAt });
    }
}
