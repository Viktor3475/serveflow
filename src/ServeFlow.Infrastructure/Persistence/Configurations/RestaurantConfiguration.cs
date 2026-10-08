using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> b)
    {
        b.ToTable("Restaurants", t =>
        {
            t.HasCheckConstraint("CK_Restaurants_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_Restaurants_Currency", "[CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.CurrencyCode).Currency();
        b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Version).IsRowVersion();
    }
}
