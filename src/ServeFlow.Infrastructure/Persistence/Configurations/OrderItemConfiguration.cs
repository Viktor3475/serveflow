using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems", t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] BETWEEN 1 AND 100");
            t.HasCheckConstraint("CK_OrderItems_Price", "[UnitPriceSnapshot] >= 0");
            t.HasCheckConstraint("CK_OrderItems_Name", "LEN(LTRIM(RTRIM([NameSnapshot]))) > 0");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id, x.MenuItemId });
        b.Property(x => x.NameSnapshot).HasMaxLength(160).IsRequired().Immutable();
        b.Property(x => x.UnitPriceSnapshot).Money().Immutable();
        b.Property(x => x.Instructions).HasMaxLength(500);
        b.HasOne<Order>().WithMany(x => x.Items).HasForeignKey(x => new { x.RestaurantId, x.OrderId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<MenuItem>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.MenuItemId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
