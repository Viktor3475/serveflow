using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class RestaurantTableConfiguration : IEntityTypeConfiguration<RestaurantTable>
{
    public void Configure(EntityTypeBuilder<RestaurantTable> b)
    {
        b.ToTable("RestaurantTables", t =>
        {
            t.HasCheckConstraint("CK_RestaurantTables_Number", "[Number] > 0");
            t.HasCheckConstraint("CK_RestaurantTables_Capacity", "[Capacity] > 0 AND [Capacity] <= 100");
            t.HasCheckConstraint("CK_RestaurantTables_Status", "[Status] IN ('Available','Occupied','Reserved')");
            t.HasCheckConstraint("CK_RestaurantTables_QrHash", "[QrTokenHash] IS NULL OR (DATALENGTH([QrTokenHash]) = 64 AND [QrTokenHash] NOT LIKE '%[^0-9a-f]%')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id });
        b.Property(x => x.QrTokenHash).Hash();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.RestaurantId, x.Number }).IsUnique();
        b.HasIndex(x => x.QrTokenHash).IsUnique().HasFilter("[QrTokenHash] IS NOT NULL");
        b.HasOne<Restaurant>().WithMany().HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.NoAction);
    }
}
