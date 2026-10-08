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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [Restaurants] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [CurrencyCode] varchar(3) NOT NULL,
        [TimeZoneId] nvarchar(100) NOT NULL,
        [IsArchived] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Restaurants] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Restaurants_Currency] CHECK ([CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'),
        CONSTRAINT [CK_Restaurants_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(160) NOT NULL,
        [IsArchived] bit NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_AspNetUsers_RestaurantId_Id] UNIQUE ([RestaurantId], [Id]),
        CONSTRAINT [FK_AspNetUsers_Restaurants_RestaurantId] FOREIGN KEY ([RestaurantId]) REFERENCES [Restaurants] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [MenuCategories] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsArchived] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_MenuCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_MenuCategories_RestaurantId_Id] UNIQUE ([RestaurantId], [Id]),
        CONSTRAINT [CK_MenuCategories_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_MenuCategories_SortOrder] CHECK ([SortOrder] >= 0),
        CONSTRAINT [FK_MenuCategories_Restaurants_RestaurantId] FOREIGN KEY ([RestaurantId]) REFERENCES [Restaurants] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [RestaurantTables] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [Capacity] int NOT NULL,
        [QrTokenHash] varchar(64) COLLATE Latin1_General_100_BIN2 NULL,
        [Status] nvarchar(16) NOT NULL,
        [IsArchived] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_RestaurantTables] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_RestaurantTables_RestaurantId_Id] UNIQUE ([RestaurantId], [Id]),
        CONSTRAINT [CK_RestaurantTables_Capacity] CHECK ([Capacity] > 0 AND [Capacity] <= 100),
        CONSTRAINT [CK_RestaurantTables_Number] CHECK ([Number] > 0),
        CONSTRAINT [CK_RestaurantTables_QrHash] CHECK ([QrTokenHash] IS NULL OR (DATALENGTH([QrTokenHash]) = 64 AND [QrTokenHash] NOT LIKE '%[^0-9a-f]%')),
        CONSTRAINT [CK_RestaurantTables_Status] CHECK ([Status] IN ('Available','Occupied','Reserved')),
        CONSTRAINT [FK_RestaurantTables_Restaurants_RestaurantId] FOREIGN KEY ([RestaurantId]) REFERENCES [Restaurants] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [MenuItems] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [MenuCategoryId] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [IsAvailable] bit NOT NULL,
        [ImageStorageKey] nvarchar(300) NULL,
        [IsArchived] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_MenuItems] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_MenuItems_RestaurantId_Id] UNIQUE ([RestaurantId], [Id]),
        CONSTRAINT [CK_MenuItems_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_MenuItems_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_MenuItems_MenuCategories_RestaurantId_MenuCategoryId] FOREIGN KEY ([RestaurantId], [MenuCategoryId]) REFERENCES [MenuCategories] ([RestaurantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [RestaurantTableId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [OrderNumber] varchar(32) NOT NULL,
        [IdempotencyKey] varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [RequestFingerprint] varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ReceiptTokenHash] varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [CurrencyCode] varchar(3) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Orders_RestaurantId_Id] UNIQUE ([RestaurantId], [Id]),
        CONSTRAINT [AK_Orders_RestaurantId_Id_CurrencyCode] UNIQUE ([RestaurantId], [Id], [CurrencyCode]),
        CONSTRAINT [CK_Orders_Currency] CHECK ([CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'),
        CONSTRAINT [CK_Orders_IdempotencyKey] CHECK (LEN(LTRIM(RTRIM([IdempotencyKey]))) > 0),
        CONSTRAINT [CK_Orders_OrderNumber] CHECK (LEN(LTRIM(RTRIM([OrderNumber]))) > 0),
        CONSTRAINT [CK_Orders_ReceiptHash] CHECK (DATALENGTH([ReceiptTokenHash]) = 64 AND [ReceiptTokenHash] NOT LIKE '%[^0-9a-f]%'),
        CONSTRAINT [CK_Orders_RequestHash] CHECK (DATALENGTH([RequestFingerprint]) = 64 AND [RequestFingerprint] NOT LIKE '%[^0-9a-f]%'),
        CONSTRAINT [CK_Orders_Status] CHECK ([Status] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')),
        CONSTRAINT [CK_Orders_Total] CHECK ([Total] >= 0),
        CONSTRAINT [FK_Orders_AspNetUsers_RestaurantId_CreatedByUserId] FOREIGN KEY ([RestaurantId], [CreatedByUserId]) REFERENCES [AspNetUsers] ([RestaurantId], [Id]),
        CONSTRAINT [FK_Orders_RestaurantTables_RestaurantId_RestaurantTableId] FOREIGN KEY ([RestaurantId], [RestaurantTableId]) REFERENCES [RestaurantTables] ([RestaurantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [MenuItemOptions] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [MenuItemId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [AdditionalPrice] decimal(18,2) NOT NULL,
        [IsAvailable] bit NOT NULL,
        [IsArchived] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_MenuItemOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_MenuItemOptions_RestaurantId_Id_MenuItemId] UNIQUE ([RestaurantId], [Id], [MenuItemId]),
        CONSTRAINT [CK_MenuItemOptions_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_MenuItemOptions_Price] CHECK ([AdditionalPrice] >= 0),
        CONSTRAINT [FK_MenuItemOptions_MenuItems_RestaurantId_MenuItemId] FOREIGN KEY ([RestaurantId], [MenuItemId]) REFERENCES [MenuItems] ([RestaurantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [MenuItemId] uniqueidentifier NOT NULL,
        [NameSnapshot] nvarchar(160) NOT NULL,
        [UnitPriceSnapshot] decimal(18,2) NOT NULL,
        [Quantity] int NOT NULL,
        [Instructions] nvarchar(500) NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OrderItems_RestaurantId_Id_MenuItemId] UNIQUE ([RestaurantId], [Id], [MenuItemId]),
        CONSTRAINT [CK_OrderItems_Name] CHECK (LEN(LTRIM(RTRIM([NameSnapshot]))) > 0),
        CONSTRAINT [CK_OrderItems_Price] CHECK ([UnitPriceSnapshot] >= 0),
        CONSTRAINT [CK_OrderItems_Quantity] CHECK ([Quantity] BETWEEN 1 AND 100),
        CONSTRAINT [FK_OrderItems_MenuItems_RestaurantId_MenuItemId] FOREIGN KEY ([RestaurantId], [MenuItemId]) REFERENCES [MenuItems] ([RestaurantId], [Id]),
        CONSTRAINT [FK_OrderItems_Orders_RestaurantId_OrderId] FOREIGN KEY ([RestaurantId], [OrderId]) REFERENCES [Orders] ([RestaurantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [OrderStatusHistory] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [ChangedByUserId] uniqueidentifier NULL,
        [PreviousStatus] nvarchar(16) NULL,
        [NewStatus] nvarchar(16) NOT NULL,
        [ChangedAt] datetimeoffset NOT NULL,
        [Reason] nvarchar(500) NULL,
        CONSTRAINT [PK_OrderStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrderStatusHistory_Change] CHECK (([PreviousStatus] IS NULL AND [NewStatus] = 'Pending') OR ([PreviousStatus] IS NOT NULL AND [PreviousStatus] <> [NewStatus])),
        CONSTRAINT [CK_OrderStatusHistory_New] CHECK ([NewStatus] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')),
        CONSTRAINT [CK_OrderStatusHistory_Previous] CHECK ([PreviousStatus] IS NULL OR [PreviousStatus] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')),
        CONSTRAINT [FK_OrderStatusHistory_AspNetUsers_RestaurantId_ChangedByUserId] FOREIGN KEY ([RestaurantId], [ChangedByUserId]) REFERENCES [AspNetUsers] ([RestaurantId], [Id]),
        CONSTRAINT [FK_OrderStatusHistory_Orders_RestaurantId_OrderId] FOREIGN KEY ([RestaurantId], [OrderId]) REFERENCES [Orders] ([RestaurantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CurrencyCode] varchar(3) NOT NULL,
        [Method] nvarchar(16) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Provider] varchar(50) NULL,
        [ProviderReference] varchar(200) COLLATE Latin1_General_100_BIN2 NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [PaidAt] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_Payments_Method] CHECK ([Method] IN ('Cash','Card','External')),
        CONSTRAINT [CK_Payments_PaidAt] CHECK (([Status] = 'Succeeded' AND [PaidAt] IS NOT NULL) OR ([Status] <> 'Succeeded' AND [PaidAt] IS NULL)),
        CONSTRAINT [CK_Payments_Provider] CHECK (([Provider] IS NULL AND [ProviderReference] IS NULL) OR (LEN(LTRIM(RTRIM([Provider]))) > 0 AND LEN(LTRIM(RTRIM([ProviderReference]))) > 0 AND [Provider] IS NOT NULL AND [ProviderReference] IS NOT NULL)),
        CONSTRAINT [CK_Payments_Status] CHECK ([Status] IN ('Pending','Succeeded','Failed')),
        CONSTRAINT [FK_Payments_Orders_RestaurantId_OrderId_CurrencyCode] FOREIGN KEY ([RestaurantId], [OrderId], [CurrencyCode]) REFERENCES [Orders] ([RestaurantId], [Id], [CurrencyCode])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE TABLE [OrderItemOptions] (
        [Id] uniqueidentifier NOT NULL,
        [RestaurantId] uniqueidentifier NOT NULL,
        [OrderItemId] uniqueidentifier NOT NULL,
        [MenuItemId] uniqueidentifier NOT NULL,
        [MenuItemOptionId] uniqueidentifier NOT NULL,
        [NameSnapshot] nvarchar(100) NOT NULL,
        [AdditionalPriceSnapshot] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderItemOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrderItemOptions_Name] CHECK (LEN(LTRIM(RTRIM([NameSnapshot]))) > 0),
        CONSTRAINT [CK_OrderItemOptions_Price] CHECK ([AdditionalPriceSnapshot] >= 0),
        CONSTRAINT [FK_OrderItemOptions_MenuItemOptions_RestaurantId_MenuItemOptionId_MenuItemId] FOREIGN KEY ([RestaurantId], [MenuItemOptionId], [MenuItemId]) REFERENCES [MenuItemOptions] ([RestaurantId], [Id], [MenuItemId]),
        CONSTRAINT [FK_OrderItemOptions_OrderItems_RestaurantId_OrderItemId_MenuItemId] FOREIGN KEY ([RestaurantId], [OrderItemId], [MenuItemId]) REFERENCES [OrderItems] ([RestaurantId], [Id], [MenuItemId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MenuCategories_RestaurantId_SortOrder] ON [MenuCategories] ([RestaurantId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MenuItemOptions_RestaurantId_MenuItemId] ON [MenuItemOptions] ([RestaurantId], [MenuItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MenuItems_RestaurantId_MenuCategoryId_IsAvailable] ON [MenuItems] ([RestaurantId], [MenuCategoryId], [IsAvailable]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderItemOptions_OrderItemId_MenuItemOptionId] ON [OrderItemOptions] ([OrderItemId], [MenuItemOptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItemOptions_RestaurantId_MenuItemOptionId_MenuItemId] ON [OrderItemOptions] ([RestaurantId], [MenuItemOptionId], [MenuItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItemOptions_RestaurantId_OrderItemId_MenuItemId] ON [OrderItemOptions] ([RestaurantId], [OrderItemId], [MenuItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_RestaurantId_MenuItemId] ON [OrderItems] ([RestaurantId], [MenuItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_RestaurantId_OrderId] ON [OrderItems] ([RestaurantId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_ReceiptTokenHash] ON [Orders] ([ReceiptTokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_RestaurantId_CreatedAt] ON [Orders] ([RestaurantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_RestaurantId_CreatedByUserId] ON [Orders] ([RestaurantId], [CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_RestaurantId_OrderNumber] ON [Orders] ([RestaurantId], [OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_RestaurantId_RestaurantTableId_IdempotencyKey] ON [Orders] ([RestaurantId], [RestaurantTableId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_RestaurantId_Status_CreatedAt] ON [Orders] ([RestaurantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistory_RestaurantId_ChangedByUserId] ON [OrderStatusHistory] ([RestaurantId], [ChangedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistory_RestaurantId_OrderId_ChangedAt] ON [OrderStatusHistory] ([RestaurantId], [OrderId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Provider_ProviderReference] ON [Payments] ([Provider], [ProviderReference]) WHERE [Provider] IS NOT NULL AND [ProviderReference] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payments_RestaurantId_OrderId_CurrencyCode] ON [Payments] ([RestaurantId], [OrderId], [CurrencyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payments_RestaurantId_Status_PaidAt] ON [Payments] ([RestaurantId], [Status], [PaidAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RestaurantTables_QrTokenHash] ON [RestaurantTables] ([QrTokenHash]) WHERE [QrTokenHash] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RestaurantTables_RestaurantId_Number] ON [RestaurantTables] ([RestaurantId], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183530_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008183530_InitialSchema', N'10.0.12');
END;

COMMIT;
GO

