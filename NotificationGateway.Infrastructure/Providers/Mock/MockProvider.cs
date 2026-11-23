using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Infrastructure.Providers.Mock;

/// <summary>Mock provider for unit/integration tests. Configurable to succeed or fail.</summary>
public class MockProvider(
    string providerCode,
    NotificationChannel channel,
    bool shouldSucceed = true,
    bool isRetryable = false,
    FailureCategory failureCategory = FailureCategory.None) : INotificationProvider
{
    public string ProviderCode => providerCode;
    public NotificationChannel Channel => channel;
    public ProviderCapabilities Capabilities => new(false, false, false);

    public Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (shouldSucceed)
            return Task.FromResult(new ProviderSendResult(true, $"mock-{Guid.NewGuid()}", "sent", false, FailureCategory.None, null, null));

        return Task.FromResult(new ProviderSendResult(false, null, "failed", isRetryable, failureCategory, "MOCK_FAIL", "Mock failure for testing"));
    }
}
