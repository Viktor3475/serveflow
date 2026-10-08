using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServeFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.Id);
                    table.CheckConstraint("CK_Restaurants_Currency", "[CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'");
                    table.CheckConstraint("CK_Restaurants_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.UniqueConstraint("AK_AspNetUsers_RestaurantId_Id", x => new { x.RestaurantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MenuCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategories", x => x.Id);
                    table.UniqueConstraint("AK_MenuCategories_RestaurantId_Id", x => new { x.RestaurantId, x.Id });
                    table.CheckConstraint("CK_MenuCategories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_MenuCategories_SortOrder", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_MenuCategories_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RestaurantTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    QrTokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantTables", x => x.Id);
                    table.UniqueConstraint("AK_RestaurantTables_RestaurantId_Id", x => new { x.RestaurantId, x.Id });
                    table.CheckConstraint("CK_RestaurantTables_Capacity", "[Capacity] > 0 AND [Capacity] <= 100");
                    table.CheckConstraint("CK_RestaurantTables_Number", "[Number] > 0");
                    table.CheckConstraint("CK_RestaurantTables_QrHash", "[QrTokenHash] IS NULL OR (DATALENGTH([QrTokenHash]) = 64 AND [QrTokenHash] NOT LIKE '%[^0-9a-f]%')");
                    table.CheckConstraint("CK_RestaurantTables_Status", "[Status] IN ('Available','Occupied','Reserved')");
                    table.ForeignKey(
                        name: "FK_RestaurantTables_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    ImageStorageKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.UniqueConstraint("AK_MenuItems_RestaurantId_Id", x => new { x.RestaurantId, x.Id });
                    table.CheckConstraint("CK_MenuItems_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_MenuItems_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuCategories_RestaurantId_MenuCategoryId",
                        columns: x => new { x.RestaurantId, x.MenuCategoryId },
                        principalTable: "MenuCategories",
                        principalColumns: new[] { "RestaurantId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantTableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderNumber = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ReceiptTokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.UniqueConstraint("AK_Orders_RestaurantId_Id", x => new { x.RestaurantId, x.Id });
                    table.UniqueConstraint("AK_Orders_RestaurantId_Id_CurrencyCode", x => new { x.RestaurantId, x.Id, x.CurrencyCode });
                    table.CheckConstraint("CK_Orders_Currency", "[CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'");
                    table.CheckConstraint("CK_Orders_IdempotencyKey", "LEN(LTRIM(RTRIM([IdempotencyKey]))) > 0");
                    table.CheckConstraint("CK_Orders_OrderNumber", "LEN(LTRIM(RTRIM([OrderNumber]))) > 0");
                    table.CheckConstraint("CK_Orders_ReceiptHash", "DATALENGTH([ReceiptTokenHash]) = 64 AND [ReceiptTokenHash] NOT LIKE '%[^0-9a-f]%'");
                    table.CheckConstraint("CK_Orders_RequestHash", "DATALENGTH([RequestFingerprint]) = 64 AND [RequestFingerprint] NOT LIKE '%[^0-9a-f]%'");
                    table.CheckConstraint("CK_Orders_Status", "[Status] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')");
                    table.CheckConstraint("CK_Orders_Total", "[Total] >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_AspNetUsers_RestaurantId_CreatedByUserId",
                        columns: x => new { x.RestaurantId, x.CreatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "RestaurantId", "Id" });
                    table.ForeignKey(
                        name: "FK_Orders_RestaurantTables_RestaurantId_RestaurantTableId",
                        columns: x => new { x.RestaurantId, x.RestaurantTableId },
                        principalTable: "RestaurantTables",
                        principalColumns: new[] { "RestaurantId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "MenuItemOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdditionalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemOptions", x => x.Id);
                    table.UniqueConstraint("AK_MenuItemOptions_RestaurantId_Id_MenuItemId", x => new { x.RestaurantId, x.Id, x.MenuItemId });
                    table.CheckConstraint("CK_MenuItemOptions_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_MenuItemOptions_Price", "[AdditionalPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_MenuItemOptions_MenuItems_RestaurantId_MenuItemId",
                        columns: x => new { x.RestaurantId, x.MenuItemId },
                        principalTable: "MenuItems",
                        principalColumns: new[] { "RestaurantId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.UniqueConstraint("AK_OrderItems_RestaurantId_Id_MenuItemId", x => new { x.RestaurantId, x.Id, x.MenuItemId });
                    table.CheckConstraint("CK_OrderItems_Name", "LEN(LTRIM(RTRIM([NameSnapshot]))) > 0");
                    table.CheckConstraint("CK_OrderItems_Price", "[UnitPriceSnapshot] >= 0");
                    table.CheckConstraint("CK_OrderItems_Quantity", "[Quantity] BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_OrderItems_MenuItems_RestaurantId_MenuItemId",
                        columns: x => new { x.RestaurantId, x.MenuItemId },
                        principalTable: "MenuItems",
                        principalColumns: new[] { "RestaurantId", "Id" });
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_RestaurantId_OrderId",
                        columns: x => new { x.RestaurantId, x.OrderId },
                        principalTable: "Orders",
                        principalColumns: new[] { "RestaurantId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_OrderStatusHistory_Change", "([PreviousStatus] IS NULL AND [NewStatus] = 'Pending') OR ([PreviousStatus] IS NOT NULL AND [PreviousStatus] <> [NewStatus])");
                    table.CheckConstraint("CK_OrderStatusHistory_New", "[NewStatus] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')");
                    table.CheckConstraint("CK_OrderStatusHistory_Previous", "[PreviousStatus] IS NULL OR [PreviousStatus] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_AspNetUsers_RestaurantId_ChangedByUserId",
                        columns: x => new { x.RestaurantId, x.ChangedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "RestaurantId", "Id" });
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_RestaurantId_OrderId",
                        columns: x => new { x.RestaurantId, x.OrderId },
                        principalTable: "Orders",
                        principalColumns: new[] { "RestaurantId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Provider = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ProviderReference = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Card','External')");
                    table.CheckConstraint("CK_Payments_PaidAt", "([Status] = 'Succeeded' AND [PaidAt] IS NOT NULL) OR ([Status] <> 'Succeeded' AND [PaidAt] IS NULL)");
                    table.CheckConstraint("CK_Payments_Provider", "([Provider] IS NULL AND [ProviderReference] IS NULL) OR (LEN(LTRIM(RTRIM([Provider]))) > 0 AND LEN(LTRIM(RTRIM([ProviderReference]))) > 0 AND [Provider] IS NOT NULL AND [ProviderReference] IS NOT NULL)");
                    table.CheckConstraint("CK_Payments_Status", "[Status] IN ('Pending','Succeeded','Failed')");
                    table.ForeignKey(
                        name: "FK_Payments_Orders_RestaurantId_OrderId_CurrencyCode",
                        columns: x => new { x.RestaurantId, x.OrderId, x.CurrencyCode },
                        principalTable: "Orders",
                        principalColumns: new[] { "RestaurantId", "Id", "CurrencyCode" });
                });

            migrationBuilder.CreateTable(
                name: "OrderItemOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdditionalPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemOptions", x => x.Id);
                    table.CheckConstraint("CK_OrderItemOptions_Name", "LEN(LTRIM(RTRIM([NameSnapshot]))) > 0");
                    table.CheckConstraint("CK_OrderItemOptions_Price", "[AdditionalPriceSnapshot] >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItemOptions_MenuItemOptions_RestaurantId_MenuItemOptionId_MenuItemId",
                        columns: x => new { x.RestaurantId, x.MenuItemOptionId, x.MenuItemId },
                        principalTable: "MenuItemOptions",
                        principalColumns: new[] { "RestaurantId", "Id", "MenuItemId" });
                    table.ForeignKey(
                        name: "FK_OrderItemOptions_OrderItems_RestaurantId_OrderItemId_MenuItemId",
                        columns: x => new { x.RestaurantId, x.OrderItemId, x.MenuItemId },
                        principalTable: "OrderItems",
                        principalColumns: new[] { "RestaurantId", "Id", "MenuItemId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_RestaurantId_SortOrder",
                table: "MenuCategories",
                columns: new[] { "RestaurantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemOptions_RestaurantId_MenuItemId",
                table: "MenuItemOptions",
                columns: new[] { "RestaurantId", "MenuItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_RestaurantId_MenuCategoryId_IsAvailable",
                table: "MenuItems",
                columns: new[] { "RestaurantId", "MenuCategoryId", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemOptions_OrderItemId_MenuItemOptionId",
                table: "OrderItemOptions",
                columns: new[] { "OrderItemId", "MenuItemOptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemOptions_RestaurantId_MenuItemOptionId_MenuItemId",
                table: "OrderItemOptions",
                columns: new[] { "RestaurantId", "MenuItemOptionId", "MenuItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemOptions_RestaurantId_OrderItemId_MenuItemId",
                table: "OrderItemOptions",
                columns: new[] { "RestaurantId", "OrderItemId", "MenuItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_RestaurantId_MenuItemId",
                table: "OrderItems",
                columns: new[] { "RestaurantId", "MenuItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_RestaurantId_OrderId",
                table: "OrderItems",
                columns: new[] { "RestaurantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ReceiptTokenHash",
                table: "Orders",
                column: "ReceiptTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_CreatedAt",
                table: "Orders",
                columns: new[] { "RestaurantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_CreatedByUserId",
                table: "Orders",
                columns: new[] { "RestaurantId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_OrderNumber",
                table: "Orders",
                columns: new[] { "RestaurantId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_RestaurantTableId_IdempotencyKey",
                table: "Orders",
                columns: new[] { "RestaurantId", "RestaurantTableId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "RestaurantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_RestaurantId_ChangedByUserId",
                table: "OrderStatusHistory",
                columns: new[] { "RestaurantId", "ChangedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_RestaurantId_OrderId_ChangedAt",
                table: "OrderStatusHistory",
                columns: new[] { "RestaurantId", "OrderId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ProviderReference",
                table: "Payments",
                columns: new[] { "Provider", "ProviderReference" },
                unique: true,
                filter: "[Provider] IS NOT NULL AND [ProviderReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RestaurantId_OrderId_CurrencyCode",
                table: "Payments",
                columns: new[] { "RestaurantId", "OrderId", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RestaurantId_Status_PaidAt",
                table: "Payments",
                columns: new[] { "RestaurantId", "Status", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_QrTokenHash",
                table: "RestaurantTables",
                column: "QrTokenHash",
                unique: true,
                filter: "[QrTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_RestaurantId_Number",
                table: "RestaurantTables",
                columns: new[] { "RestaurantId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "OrderItemOptions");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "MenuItemOptions");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "MenuCategories");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "RestaurantTables");

            migrationBuilder.DropTable(
                name: "Restaurants");
        }
    }
}
