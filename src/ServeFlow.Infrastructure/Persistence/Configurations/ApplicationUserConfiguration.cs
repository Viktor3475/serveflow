using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;
using ServeFlow.Infrastructure.Identity;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id });
        b.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        b.HasOne<Restaurant>().WithMany().HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.NoAction);
    }
}
