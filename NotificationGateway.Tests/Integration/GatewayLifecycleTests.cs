using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationGateway.API.Authentication;
using NotificationGateway.API.Controllers;
using NotificationGateway.API.Middleware;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Application.Notifications.SubmitNotification;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Infrastructure.BackgroundJobs;
using NotificationGateway.Infrastructure.Persistence;
using NotificationGateway.Infrastructure.Persistence.Repositories;
using NotificationGateway.Infrastructure.Providers.Push;
using NotificationGateway.Infrastructure.Providers.Voice;
using NotificationGateway.Infrastructure.Security;
using NotificationGateway.Infrastructure.Services;

namespace NotificationGateway.Tests.Integration;

public class GatewayLifecycleTests
{
    private static NotificationGatewayDbContext CreateInMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<NotificationGatewayDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new NotificationGatewayDbContext(opts);
    }

    // 1. Cross-Tenant Security & API Key Enforcement
    [Fact]
    public async Task TenantMiddleware_RejectsCrossTenantHeaderSpoofing_With403()
    {
        await using var db = CreateInMemoryDb();
        var hasher = new Pbkdf2PasswordHasher();
        var jwtTenant = Guid.NewGuid();
        var spoofedTenant = Guid.NewGuid();
        var appId = Guid.NewGuid();

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Tenant-Id"] = spoofedTenant.ToString();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Customer"),
            new Claim("tenant_id", jwtTenant.ToString()),
            new Claim("app_id", appId.ToString())
        }, "Bearer"));

        var notifCtx = new NotificationContext();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(ctx, notifCtx, db, hasher);

        nextCalled.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TenantMiddleware_ValidatesXApiKey_AndBlocksInvalidKey()
    {
        await using var db = CreateInMemoryDb();
        var hasher = new Pbkdf2PasswordHasher();
        var tenantId = Guid.NewGuid();
        var rawKey = "ng_live_secret_test_key_123";
        var client = ApplicationClient.Create(tenantId, "Svc", "svc-1", hasher.HashPassword(rawKey), "ng_live_");
        db.ApplicationClients.Add(client);
        await db.SaveChangesAsync();

        var notifCtx = new NotificationContext();
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        // Valid key
        var okCtx = new DefaultHttpContext();
        okCtx.Request.Headers["X-Api-Key"] = rawKey;
        await middleware.InvokeAsync(okCtx, notifCtx, db, hasher);
        notifCtx.TenantId.Should().Be(tenantId);
        okCtx.User.Identity?.IsAuthenticated.Should().BeTrue();

        // Invalid key
        var badCtx = new DefaultHttpContext();
        badCtx.Request.Headers["X-Api-Key"] = "ng_live_wrong_key";
        await middleware.InvokeAsync(badCtx, new NotificationContext(), db, hasher);
        badCtx.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // 2. Credential Encryption at Rest (AES-256-CBC) in ProviderConfigurationsController
    [Fact]
    public async Task ProviderConfig_UpsertEncryptsAtRest_AndDecryptsOnRead()
    {
        await using var db = CreateInMemoryDb();
        var encryptor = new AesCredentialEncryptor(Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()));
        var repo = new ProviderConfigurationRepository(db);
        var uow = new UnitOfWork(db);
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var notifCtx = new NotificationContext { TenantId = tenantId, ApplicationId = appId, ClientId = userId.ToString() };
        var controller = new ProviderConfigurationsController(notifCtx, repo, db, encryptor, uow);

        var configJson = JsonDocument.Parse("{\"ApiKey\":\"ded500b6\",\"ApiSecret\":\"super_secret_123\"}").RootElement;
        await controller.Upsert(new ProviderConfigurationsController.UpsertProviderConfigRequest(
            "SMS", NotificationChannel.Sms, configJson), CancellationToken.None);

        var stored = await db.TenantProviderConfigurations.FirstAsync();
        stored.ConfigurationJson.Should().Be("{}"); // No plaintext secret stored
        stored.EncryptedCredentials.Should().NotBeNullOrEmpty();
        stored.EncryptedCredentials.Should().NotContain("super_secret_123");

        var getResult = await controller.GetAll(null, CancellationToken.None);
        var okObj = getResult.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okObj.Value);
        json.Should().Contain("super_secret_123");
    }

    // 3. Secondary-Provider Failover Execution
    [Fact]
    public async Task DeliveryDispatcher_FailsOverToSecondaryProvider_WhenPrimaryFails()
    {
        await using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var encryptor = new AesCredentialEncryptor(Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()));

        var notifType = NotificationTypeDefinition.Create(tenantId, appId, "ALERT", "Alert", "{}", new[] { NotificationChannel.Email });
        var tpl = NotificationTemplate.Create(tenantId, appId, notifType.Id, NotificationChannel.Email, "en", "{\"content\":\"Hi {{name}}\"}", "Subj");
        tpl.Publish();
        var notif = Notification.Create(tenantId, appId, notifType.Id, "ALERT", 1, "{\"name\":\"Dina\"}", NotificationPriority.High, language: "en");
        var delivery = NotificationDelivery.Create(tenantId, notif.Id, NotificationChannel.Email, "dina@example.com");

        db.NotificationTypeDefinitions.Add(notifType);
        db.NotificationTemplates.Add(tpl);
        db.Notifications.Add(notif);
        db.NotificationDeliveries.Add(delivery);
        await db.SaveChangesAsync();

        var primaryCfg = TenantProviderConfiguration.Create(tenantId, appId, "PRIMARY_SMTP", NotificationChannel.Email, "{}", isPrimary: true);
        var fallbackCfg = TenantProviderConfiguration.Create(tenantId, appId, "BACKUP_SMTP", NotificationChannel.Email, "{}", isPrimary: false);

        var primaryMock = new Mock<INotificationProvider>();
        primaryMock.Setup(p => p.ProviderCode).Returns("PRIMARY_SMTP");
        primaryMock.Setup(p => p.SendAsync(It.IsAny<RenderedNotification>(), It.IsAny<TenantProviderConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "ERR_DOWN", "Primary down"));

        var fallbackMock = new Mock<INotificationProvider>();
        fallbackMock.Setup(p => p.ProviderCode).Returns("BACKUP_SMTP");
        fallbackMock.Setup(p => p.SendAsync(It.IsAny<RenderedNotification>(), It.IsAny<TenantProviderConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSendResult(true, "backup_msg_1", "sent", false, FailureCategory.None, null, null));

        var resolverMock = new Mock<IProviderResolver>();
        resolverMock.Setup(r => r.ResolveAsync(tenantId, appId, NotificationChannel.Email, notifType.Id, NotificationPriority.High, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(primaryCfg);
        resolverMock.Setup(r => r.ResolveFallbacksAsync(tenantId, appId, NotificationChannel.Email, "PRIMARY_SMTP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fallbackCfg });
        resolverMock.Setup(r => r.GetProvider("PRIMARY_SMTP")).Returns(primaryMock.Object);
        resolverMock.Setup(r => r.GetProvider("BACKUP_SMTP")).Returns(fallbackMock.Object);

        var jobClientMock = new Mock<IBackgroundJobClient>();
        var dispatcher = new DeliveryDispatcher(
            new DeliveryRepository(db), new NotificationRepository(db),
            new HandlebarsTemplateRenderer(new TemplateRepository(db)), new NotificationTypeRepository(db),
            resolverMock.Object, encryptor, jobClientMock.Object, new UnitOfWork(db),
            NullLogger<DeliveryDispatcher>.Instance);

        await dispatcher.DispatchDeliveryAsync(tenantId, delivery.Id);

        var updated = await db.NotificationDeliveries.Include(d => d.Attempts).FirstAsync(d => d.Id == delivery.Id);
        updated.Status.Should().Be(DeliveryStatus.Sent);
        updated.AttemptCount.Should().Be(2);
        updated.Attempts.Should().Contain(a => a.ProviderCode == "PRIMARY_SMTP" && a.Status == DeliveryStatus.Failed);
        updated.Attempts.Should().Contain(a => a.ProviderCode == "BACKUP_SMTP" && a.Status == DeliveryStatus.Sent);
    }

    // 4. Retry Scheduling via Hangfire + Recovery
    [Fact]
    public async Task DeliveryDispatcher_SchedulesHangfireRetryOnTransientFailure_AndRecovers()
    {
        await using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var encryptor = new AesCredentialEncryptor(Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()));

        var notifType = NotificationTypeDefinition.Create(tenantId, appId, "OTP", "OTP", "{}", new[] { NotificationChannel.Sms });
        var tpl = NotificationTemplate.Create(tenantId, appId, notifType.Id, NotificationChannel.Sms, "ar", "{\"message\":\"رمزك {{code}}\"}");
        tpl.Publish();
        var notif = Notification.Create(tenantId, appId, notifType.Id, "OTP", 1, "{\"code\":\"1234\"}", NotificationPriority.High, language: "ar");
        var delivery = NotificationDelivery.Create(tenantId, notif.Id, NotificationChannel.Sms, "+201000000000");

        db.NotificationTypeDefinitions.Add(notifType);
        db.NotificationTemplates.Add(tpl);
        db.Notifications.Add(notif);
        db.NotificationDeliveries.Add(delivery);
        await db.SaveChangesAsync();

        var cfg = TenantProviderConfiguration.Create(tenantId, appId, "SMS", NotificationChannel.Sms, "{}");
        var callCount = 0;
        string? capturedBody = null;

        var smsMock = new Mock<INotificationProvider>();
        smsMock.Setup(p => p.ProviderCode).Returns("SMS");
        smsMock.Setup(p => p.SendAsync(It.IsAny<RenderedNotification>(), It.IsAny<TenantProviderConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<RenderedNotification, TenantProviderConfiguration, CancellationToken>((n, _, _) => capturedBody = n.Body)
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "TEMP", "Timeout")
                    : new ProviderSendResult(true, "SM_RECOVERED", "sent", false, FailureCategory.None, null, null);
            });

        var resolverMock = new Mock<IProviderResolver>();
        resolverMock.Setup(r => r.ResolveAsync(tenantId, appId, NotificationChannel.Sms, notifType.Id, NotificationPriority.High, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(cfg);
        resolverMock.Setup(r => r.ResolveFallbacksAsync(tenantId, appId, NotificationChannel.Sms, "SMS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantProviderConfiguration>());
        resolverMock.Setup(r => r.GetProvider("SMS")).Returns(smsMock.Object);

        var jobClientMock = new Mock<IBackgroundJobClient>();
        var dispatcher = new DeliveryDispatcher(
            new DeliveryRepository(db), new NotificationRepository(db),
            new HandlebarsTemplateRenderer(new TemplateRepository(db)), new NotificationTypeRepository(db),
            resolverMock.Object, encryptor, jobClientMock.Object, new UnitOfWork(db),
            NullLogger<DeliveryDispatcher>.Instance);

        // First attempt fails -> schedules Hangfire retry
        await dispatcher.DispatchDeliveryAsync(tenantId, delivery.Id);
        var retrying = await db.NotificationDeliveries.FirstAsync(d => d.Id == delivery.Id);
        retrying.Status.Should().Be(DeliveryStatus.Retrying);
        retrying.NextAttemptAt.Should().NotBeNull();
        capturedBody.Should().Be("رمزك 1234"); // Verified Arabic localization!
        jobClientMock.Verify(j => j.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()), Times.Once);

        // Second attempt recovers -> Sent
        await dispatcher.DispatchDeliveryAsync(tenantId, delivery.Id);
        var recovered = await db.NotificationDeliveries.FirstAsync(d => d.Id == delivery.Id);
        recovered.Status.Should().Be(DeliveryStatus.Sent);
        recovered.AttemptCount.Should().Be(2);
    }

    // 5. UserPreference Opt-Out + Push & Voice Providers + Concurrent Idempotency
    [Fact]
    public async Task OutboxProcessor_EnforcesUserPreferenceOptOut()
    {
        await using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var notifType = NotificationTypeDefinition.Create(tenantId, appId, "MARKETING", "Promo", "{}", new[] { NotificationChannel.Email, NotificationChannel.Push });
        var notif = Notification.Create(tenantId, appId, notifType.Id, "MARKETING", 1, "{}", NotificationPriority.Normal);
        var outbox = NotificationOutbox.Create(tenantId, notif.Id, "NotificationAccepted",
            JsonSerializer.Serialize(new { notif.Id, Channels = new[] { NotificationChannel.Email, NotificationChannel.Push }, Language = "en", Recipient = new { ExternalUserId = "cust-optout", Email = "a@b.com", DeviceToken = "dev-1" } }));

        var pref = UserPreference.Create(tenantId, appId, "cust-optout");
        pref.PreferredChannels.Add(NotificationChannel.Push); // Opted out of Email, only wants Push

        db.NotificationTypeDefinitions.Add(notifType);
        db.Notifications.Add(notif);
        db.NotificationOutbox.Add(outbox);
        db.UserPreferences.Add(pref);
        await db.SaveChangesAsync();

        var jobClientMock = new Mock<IBackgroundJobClient>();
        var processor = new OutboxProcessor(
            new OutboxRepository(db), new DeliveryRepository(db), new NotificationRepository(db),
            new UserPreferenceRepository(db), new UnitOfWork(db), jobClientMock.Object,
            NullLogger<OutboxProcessor>.Instance);

        await processor.ProcessAsync();

        var deliveries = await db.NotificationDeliveries.ToListAsync();
        deliveries.Should().HaveCount(1);
        deliveries[0].Channel.Should().Be(NotificationChannel.Push);
    }

    [Fact]
    public async Task PushAndVoiceProviders_SimulatedSend_Succeed()
    {
        var push = new PushNotificationProvider(new HttpClient(), NullLogger<PushNotificationProvider>.Instance);
        var voice = new VoiceNotificationProvider(new HttpClient(), NullLogger<VoiceNotificationProvider>.Instance);
        var cfg = TenantProviderConfiguration.Create(Guid.NewGuid(), Guid.NewGuid(), "TEST", NotificationChannel.Push, "{}");

        var pushRes = await push.SendAsync(new RenderedNotification(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), NotificationChannel.Push, "device_token_1", "Hi", "Alert", null), cfg, CancellationToken.None);
        var voiceRes = await voice.SendAsync(new RenderedNotification(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), NotificationChannel.Voice, "+201000000000", null, "Your code is 1234", null), cfg, CancellationToken.None);

        pushRes.IsSuccess.Should().BeTrue();
        pushRes.ProviderMessageId.Should().StartWith("fcm.sim_");
        voiceRes.IsSuccess.Should().BeTrue();
        voiceRes.ProviderMessageId.Should().StartWith("call.sim_");
    }
}
