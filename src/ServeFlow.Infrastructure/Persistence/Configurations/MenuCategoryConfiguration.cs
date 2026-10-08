using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> b)
    {
        b.ToTable("MenuCategories", t =>
        {
            t.HasCheckConstraint("CK_MenuCategories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_MenuCategories_SortOrder", "[SortOrder] >= 0");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id });
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.RestaurantId, x.SortOrder });
        b.HasOne<Restaurant>().WithMany().HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.NoAction);
    }
}
