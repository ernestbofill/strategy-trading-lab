using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StrategyTradingLab.Api.Data;

static class ModelBuilderExtensions
{
    /// <summary><c>CHECK (column IN (...))</c> over every value of the enum.</summary>
    public static void HasEnumCheck<TEnum>(this TableBuilder table, string column) where TEnum : struct, Enum
    {
        var values = string.Join(", ", SnakeCaseEnumConverter<TEnum>.StoredValues.Select(value => $"'{value}'"));
        table.HasCheckConstraint($"ck_{table.Name}_{column}", $"{column} IN ({values})");
    }

    /// <summary><c>created_at timestamptz not null default now()</c>.</summary>
    public static void HasCreatedAt<T>(this EntityTypeBuilder<T> entity) where T : class =>
        entity.Property<DateTimeOffset>("CreatedAt").HasDefaultValueSql("now()");

    /// <summary>
    /// A column default the database fills when a row is inserted by SQL. EF always sends the
    /// property's value, so a C# <c>false</c> isn't mistaken for "unset" and replaced by the default.
    /// </summary>
    public static PropertyBuilder<TProperty> HasColumnDefault<TProperty>(
        this PropertyBuilder<TProperty> property, TProperty value) =>
        property.HasDefaultValue(value).ValueGeneratedNever();
}
