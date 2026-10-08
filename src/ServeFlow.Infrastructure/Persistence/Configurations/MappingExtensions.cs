using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ServeFlow.Infrastructure.Persistence.Configurations;

internal static class MappingExtensions
{
    internal static PropertyBuilder<decimal> Money(this PropertyBuilder<decimal> property) => property.HasPrecision(18, 2);

    internal static PropertyBuilder<string> Currency(this PropertyBuilder<string> property) =>
        property.HasMaxLength(3).IsUnicode(false).IsRequired();

    internal static PropertyBuilder<T> Hash<T>(this PropertyBuilder<T> property) =>
        property.HasMaxLength(64).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");

    internal static PropertyBuilder<T> Immutable<T>(this PropertyBuilder<T> property)
    {
        property.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        return property;
    }
}
