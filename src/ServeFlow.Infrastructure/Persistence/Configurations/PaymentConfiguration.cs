using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Payments_Status", "[Status] IN ('Pending','Succeeded','Failed')");
            t.HasCheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Card','External')");
            t.HasCheckConstraint("CK_Payments_PaidAt", "([Status] = 'Succeeded' AND [PaidAt] IS NOT NULL) OR ([Status] <> 'Succeeded' AND [PaidAt] IS NULL)");
            t.HasCheckConstraint("CK_Payments_Provider", "([Provider] IS NULL AND [ProviderReference] IS NULL) OR (LEN(LTRIM(RTRIM([Provider]))) > 0 AND LEN(LTRIM(RTRIM([ProviderReference]))) > 0 AND [Provider] IS NOT NULL AND [ProviderReference] IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).Money().Immutable();
        b.Property(x => x.CurrencyCode).Currency().Immutable();
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(16).Immutable();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Provider).HasMaxLength(50).IsUnicode(false).Immutable();
        b.Property(x => x.ProviderReference).HasMaxLength(200).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").Immutable();
        b.Property(x => x.CreatedAt).Immutable();
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.Provider, x.ProviderReference }).IsUnique()
            .HasFilter("[Provider] IS NOT NULL AND [ProviderReference] IS NOT NULL");
        b.HasIndex(x => new { x.RestaurantId, x.Status, x.PaidAt });
        b.HasOne<Order>().WithMany(x => x.Payments).HasForeignKey(x => new { x.RestaurantId, x.OrderId, x.CurrencyCode })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id, x.CurrencyCode }).OnDelete(DeleteBehavior.NoAction);
    }
}
