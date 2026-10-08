using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class MenuItemOptionConfiguration : IEntityTypeConfiguration<MenuItemOption>
{
    public void Configure(EntityTypeBuilder<MenuItemOption> b)
    {
        b.ToTable("MenuItemOptions", t =>
        {
            t.HasCheckConstraint("CK_MenuItemOptions_Price", "[AdditionalPrice] >= 0");
            t.HasCheckConstraint("CK_MenuItemOptions_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id, x.MenuItemId });
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.AdditionalPrice).Money();
        b.Property(x => x.Version).IsRowVersion();
        b.HasOne<MenuItem>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.MenuItemId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
