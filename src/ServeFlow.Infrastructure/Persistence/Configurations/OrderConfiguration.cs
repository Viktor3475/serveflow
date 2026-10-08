using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;
using ServeFlow.Infrastructure.Identity;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Total", "[Total] >= 0");
            t.HasCheckConstraint("CK_Orders_Status", "[Status] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')");
            t.HasCheckConstraint("CK_Orders_Currency", "[CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'");
            t.HasCheckConstraint("CK_Orders_OrderNumber", "LEN(LTRIM(RTRIM([OrderNumber]))) > 0");
            t.HasCheckConstraint("CK_Orders_IdempotencyKey", "LEN(LTRIM(RTRIM([IdempotencyKey]))) > 0");
            t.HasCheckConstraint("CK_Orders_RequestHash", "DATALENGTH([RequestFingerprint]) = 64 AND [RequestFingerprint] NOT LIKE '%[^0-9a-f]%'");
            t.HasCheckConstraint("CK_Orders_ReceiptHash", "DATALENGTH([ReceiptTokenHash]) = 64 AND [ReceiptTokenHash] NOT LIKE '%[^0-9a-f]%'");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id });
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id, x.CurrencyCode });
        b.Property(x => x.OrderNumber).HasMaxLength(32).IsUnicode(false).IsRequired().Immutable();
        b.Property(x => x.IdempotencyKey).HasMaxLength(64).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired().Immutable();
        b.Property(x => x.RequestFingerprint).Hash().IsRequired().Immutable();
        b.Property(x => x.ReceiptTokenHash).Hash().IsRequired().Immutable();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CurrencyCode).Currency().Immutable();
        b.Property(x => x.Total).Money();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).Immutable();
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.RestaurantId, x.OrderNumber }).IsUnique();
        b.HasIndex(x => new { x.RestaurantId, x.RestaurantTableId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => x.ReceiptTokenHash).IsUnique();
        b.HasIndex(x => new { x.RestaurantId, x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.RestaurantId, x.CreatedAt });
        b.HasOne<RestaurantTable>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.RestaurantTableId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.CreatedByUserId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
