USE [NotificationGatewayDb];
GO

-- 1. Create __EFMigrationsHistory table
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

-- 2. AuditLogs
IF OBJECT_ID(N'[AuditLogs]') IS NULL
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NULL,
        [Action] nvarchar(max) NOT NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [EntityId] uniqueidentifier NULL,
        [ActorClientId] nvarchar(max) NULL,
        [ChangesJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

-- 3. NotificationOutbox
IF OBJECT_ID(N'[NotificationOutbox]') IS NULL
BEGIN
    CREATE TABLE [NotificationOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [NotificationId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [IsProcessed] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [RetryCount] int NOT NULL,
        CONSTRAINT [PK_NotificationOutbox] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_NotificationOutbox_IsProcessed_CreatedAt] ON [NotificationOutbox] ([IsProcessed], [CreatedAt]);
END;
GO

-- 4. ProviderDefinitions
IF OBJECT_ID(N'[ProviderDefinitions]') IS NULL
BEGIN
    CREATE TABLE [ProviderDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [DisplayName] nvarchar(max) NOT NULL,
        [Channel] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_ProviderDefinitions] PRIMARY KEY ([Id])
    );
END;
GO

-- 5. RoutingRules
IF OBJECT_ID(N'[RoutingRules]') IS NULL
BEGIN
    CREATE TABLE [RoutingRules] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [NotificationTypeDefinitionId] uniqueidentifier NULL,
        [Priority] nvarchar(20) NULL,
        [Channel] nvarchar(50) NOT NULL,
        [PrimaryProviderCode] nvarchar(100) NOT NULL,
        [FallbackProviderCodes] nvarchar(max) NOT NULL,
        [FallbackPolicy] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [Order] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_RoutingRules] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_RoutingRules_TenantId_ApplicationId_Channel_IsActive] ON [RoutingRules] ([TenantId], [ApplicationId], [Channel], [IsActive]);
END;
GO

-- 6. TenantProviderConfigurations
IF OBJECT_ID(N'[TenantProviderConfigurations]') IS NULL
BEGIN
    CREATE TABLE [TenantProviderConfigurations] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [ProviderCode] nvarchar(100) NOT NULL,
        [Channel] nvarchar(50) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [IsPrimary] bit NOT NULL,
        [Priority] int NOT NULL,
        [ConfigurationJson] nvarchar(max) NOT NULL,
        [EncryptedCredentials] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantProviderConfigurations] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_TenantProviderConfigurations_TenantId_ApplicationId_Channel] ON [TenantProviderConfigurations] ([TenantId], [ApplicationId], [Channel]);
END;
GO

-- 7. Tenants
IF OBJECT_ID(N'[Tenants]') IS NULL
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Tenants_Slug] ON [Tenants] ([Slug]);
END;
GO

-- 8. UserPreferences
IF OBJECT_ID(N'[UserPreferences]') IS NULL
BEGIN
    CREATE TABLE [UserPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [ExternalUserId] nvarchar(max) NOT NULL,
        [PreferredLanguage] nvarchar(max) NULL,
        [PreferredChannels] nvarchar(max) NOT NULL,
        [DisabledNotificationTypes] nvarchar(max) NOT NULL,
        [QuietHoursEnabled] bit NOT NULL,
        [QuietHoursStart] time NULL,
        [QuietHoursEnd] time NULL,
        [QuietHoursTimezone] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id])
    );
END;
GO

-- 9. ApplicationClients
IF OBJECT_ID(N'[ApplicationClients]') IS NULL
BEGIN
    CREATE TABLE [ApplicationClients] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ClientId] nvarchar(100) NOT NULL,
        [HashedApiKey] nvarchar(512) NOT NULL,
        [ApiKeyPrefix] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [RevokedAt] datetime2 NULL,
        CONSTRAINT [PK_ApplicationClients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApplicationClients_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_ApplicationClients_TenantId_ClientId] ON [ApplicationClients] ([TenantId], [ClientId]);
END;
GO

-- 10. Users
IF OBJECT_ID(N'[Users]') IS NULL
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Role] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_ApplicationClients_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [ApplicationClients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_Users_TenantId_ApplicationId_Email] ON [Users] ([TenantId], [ApplicationId], [Email]);
    CREATE INDEX [IX_Users_ApplicationId] ON [Users] ([ApplicationId]);
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;
GO

-- 11. NotificationTypeDefinitions
IF OBJECT_ID(N'[NotificationTypeDefinitions]') IS NULL
BEGIN
    CREATE TABLE [NotificationTypeDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [PayloadSchemaJson] nvarchar(max) NOT NULL,
        [SchemaVersion] int NOT NULL,
        [AllowedChannels] nvarchar(max) NOT NULL,
        [DefaultLanguage] nvarchar(10) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_NotificationTypeDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationTypeDefinitions_ApplicationClients_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [ApplicationClients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NotificationTypeDefinitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_NotificationTypeDefinitions_TenantId_ApplicationId_Code] ON [NotificationTypeDefinitions] ([TenantId], [ApplicationId], [Code]);
    CREATE INDEX [IX_NotificationTypeDefinitions_ApplicationId] ON [NotificationTypeDefinitions] ([ApplicationId]);
    CREATE INDEX [IX_NotificationTypeDefinitions_TenantId] ON [NotificationTypeDefinitions] ([TenantId]);
END;
GO

-- 12. Notifications
IF OBJECT_ID(N'[Notifications]') IS NULL
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [NotificationTypeDefinitionId] uniqueidentifier NOT NULL,
        [NotificationTypeCode] nvarchar(100) NOT NULL,
        [SchemaVersion] int NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [IdempotencyKey] nvarchar(256) NULL,
        [CorrelationId] nvarchar(256) NULL,
        [MetadataJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ScheduledAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Notifications_TenantId_ApplicationId_IdempotencyKey] ON [Notifications] ([TenantId], [ApplicationId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL;
    CREATE INDEX [IX_Notifications_TenantId_ApplicationId_CreatedAt] ON [Notifications] ([TenantId], [ApplicationId], [CreatedAt]);
END;
GO

-- 13. NotificationTemplates
IF OBJECT_ID(N'[NotificationTemplates]') IS NULL
BEGIN
    CREATE TABLE [NotificationTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [NotificationTypeDefinitionId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Channel] nvarchar(50) NOT NULL,
        [Language] nvarchar(10) NOT NULL,
        [Version] int NOT NULL,
        [Subject] nvarchar(500) NULL,
        [ContentJson] nvarchar(max) NOT NULL,
        [Variables] nvarchar(max) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [PublishedAt] datetime2 NULL,
        CONSTRAINT [PK_NotificationTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationTemplates_NotificationTypeDefinitions_NotificationTypeDefinitionId] FOREIGN KEY ([NotificationTypeDefinitionId]) REFERENCES [NotificationTypeDefinitions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NotificationTemplates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
    CREATE INDEX [IX_NotificationTemplates_NotificationTypeDefinitionId] ON [NotificationTemplates] ([NotificationTypeDefinitionId]);
    CREATE INDEX [IX_NotificationTemplates_UserId] ON [NotificationTemplates] ([UserId]);
END;
GO

-- 14. NotificationDeliveries
IF OBJECT_ID(N'[NotificationDeliveries]') IS NULL
BEGIN
    CREATE TABLE [NotificationDeliveries] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [NotificationId] uniqueidentifier NOT NULL,
        [Channel] nvarchar(50) NOT NULL,
        [RecipientExternalUserId] nvarchar(max) NULL,
        [RecipientAddress] nvarchar(500) NOT NULL,
        [TemplateVersionId] uniqueidentifier NULL,
        [ProviderConfigurationId] uniqueidentifier NULL,
        [Status] nvarchar(30) NOT NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_NotificationDeliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationDeliveries_Notifications_NotificationId] FOREIGN KEY ([NotificationId]) REFERENCES [Notifications] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_NotificationDeliveries_NotificationId] ON [NotificationDeliveries] ([NotificationId]);
    CREATE INDEX [IX_NotificationDeliveries_Status_NextAttemptAt] ON [NotificationDeliveries] ([Status], [NextAttemptAt]);
    CREATE INDEX [IX_NotificationDeliveries_TenantId_NotificationId] ON [NotificationDeliveries] ([TenantId], [NotificationId]);
END;
GO

-- 15. DeliveryAttempts
IF OBJECT_ID(N'[DeliveryAttempts]') IS NULL
BEGIN
    CREATE TABLE [DeliveryAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [DeliveryId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [ProviderCode] nvarchar(100) NOT NULL,
        [ProviderMessageId] nvarchar(500) NULL,
        [Status] nvarchar(30) NOT NULL,
        [FailureCategory] nvarchar(50) NOT NULL,
        [ErrorCode] nvarchar(100) NULL,
        [SafeErrorMessage] nvarchar(1000) NULL,
        [IsRetryable] bit NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [DurationMilliseconds] int NULL,
        CONSTRAINT [PK_DeliveryAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryAttempts_NotificationDeliveries_DeliveryId] FOREIGN KEY ([DeliveryId]) REFERENCES [NotificationDeliveries] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_DeliveryAttempts_DeliveryId] ON [DeliveryAttempts] ([DeliveryId]);
END;
GO

-- Record Migration in history
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261124071205_InitialCreate', N'9.0.0');
GO

COMMIT;
GO
