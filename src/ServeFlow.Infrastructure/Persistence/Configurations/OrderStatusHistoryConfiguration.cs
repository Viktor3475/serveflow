using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;
using ServeFlow.Infrastructure.Identity;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("OrderStatusHistory", t =>
        {
            t.HasCheckConstraint("CK_OrderStatusHistory_Previous", "[PreviousStatus] IS NULL OR [PreviousStatus] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')");
            t.HasCheckConstraint("CK_OrderStatusHistory_New", "[NewStatus] IN ('Pending','Accepted','Preparing','Ready','Served','Completed','Cancelled')");
            t.HasCheckConstraint("CK_OrderStatusHistory_Change", "([PreviousStatus] IS NULL AND [NewStatus] = 'Pending') OR ([PreviousStatus] IS NOT NULL AND [PreviousStatus] <> [NewStatus])");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(16).Immutable();
        b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(16).Immutable();
        b.Property(x => x.ChangedAt).Immutable();
        b.Property(x => x.Reason).HasMaxLength(500).Immutable();
        b.Property(x => x.ChangedByUserId).Immutable();
        b.HasIndex(x => new { x.RestaurantId, x.OrderId, x.ChangedAt });
        b.HasOne<Order>().WithMany(x => x.StatusHistory).HasForeignKey(x => new { x.RestaurantId, x.OrderId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.ChangedByUserId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
