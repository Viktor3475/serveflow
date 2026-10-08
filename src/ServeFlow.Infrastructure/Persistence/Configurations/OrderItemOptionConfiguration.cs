using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class OrderItemOptionConfiguration : IEntityTypeConfiguration<OrderItemOption>
{
    public void Configure(EntityTypeBuilder<OrderItemOption> b)
    {
        b.ToTable("OrderItemOptions", t =>
        {
            t.HasCheckConstraint("CK_OrderItemOptions_Price", "[AdditionalPriceSnapshot] >= 0");
            t.HasCheckConstraint("CK_OrderItemOptions_Name", "LEN(LTRIM(RTRIM([NameSnapshot]))) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.NameSnapshot).HasMaxLength(100).IsRequired().Immutable();
        b.Property(x => x.AdditionalPriceSnapshot).Money().Immutable();
        b.HasIndex(x => new { x.OrderItemId, x.MenuItemOptionId }).IsUnique();
        b.HasOne<OrderItem>().WithMany(x => x.Options).HasForeignKey(x => new { x.RestaurantId, x.OrderItemId, x.MenuItemId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id, x.MenuItemId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<MenuItemOption>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.MenuItemOptionId, x.MenuItemId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id, x.MenuItemId }).OnDelete(DeleteBehavior.NoAction);
    }
}
