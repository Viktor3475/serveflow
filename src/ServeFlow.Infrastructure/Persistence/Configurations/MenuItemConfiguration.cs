using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> b)
    {
        b.ToTable("MenuItems", t =>
        {
            t.HasCheckConstraint("CK_MenuItems_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_MenuItems_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id });
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Price).Money();
        b.Property(x => x.ImageStorageKey).HasMaxLength(300);
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.RestaurantId, x.MenuCategoryId, x.IsAvailable });
        b.HasOne<MenuCategory>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.MenuCategoryId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
