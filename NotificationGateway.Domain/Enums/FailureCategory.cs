namespace NotificationGateway.Domain.Enums;

public enum FailureCategory
{
    None,
    InvalidRecipient,
    InvalidPayload,
    InvalidCredentials,
    ProviderUnavailable,
    RateLimit,
    TimeoutBeforeSend,
    TimeoutUncertain,
    ProviderRejected,
    Unknown
}
