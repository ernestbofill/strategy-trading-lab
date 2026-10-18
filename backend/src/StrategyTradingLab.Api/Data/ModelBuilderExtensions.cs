using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Data;

static class ModelBuilderExtensions
{
    /// <summary><c>CHECK (column IN (...))</c>, named <c>ck_{table}_{column}</c>.</summary>
    public static void HasInCheck(this TableBuilder table, string column, IEnumerable<string> values) =>
        table.HasCheckConstraint($"ck_{table.Name}_{column}", InList(column, values));

    /// <summary>The sign checks both trade tables share.</summary>
    public static void HasTradeSignChecks(this TableBuilder table)
    {
        table.HasCheckConstraint($"ck_{table.Name}_price", "price > 0");
        table.HasCheckConstraint($"ck_{table.Name}_shares", "shares > 0");
        table.HasCheckConstraint($"ck_{table.Name}_amount", "amount > 0");
        table.HasCheckConstraint($"ck_{table.Name}_fee", "fee >= 0");
        table.HasCheckConstraint($"ck_{table.Name}_fx_rate", "fx_rate > 0");
    }

    /// <summary>
    /// A column default the database fills when a row is inserted by SQL. EF always sends the
    /// property's value, so a C# <c>false</c> isn't mistaken for "unset" and replaced by the default.
    /// </summary>
    public static PropertyBuilder<TProperty> HasColumnDefault<TProperty>(
        this PropertyBuilder<TProperty> property, TProperty value) =>
        property.HasDefaultValue(value).ValueGeneratedNever();

    /// <summary>
    /// Rules every table follows, applied after the per-table configurations:
    /// <c>created_at</c> and <c>updated_at</c> default to <c>now()</c>, and every enum column
    /// gets a CHECK on the values SnakeCaseEnumConverter stores.
    /// </summary>
    public static void ApplyTableConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.FindProperty(nameof(Asset.CreatedAt))?.SetDefaultValueSql("now()");
            entity.FindProperty(nameof(IHasUpdatedAt.UpdatedAt))?.SetDefaultValueSql("now()");

            var table = entity.GetTableName();
            foreach (var property in entity.GetProperties())
            {
                var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (!SnakeCaseEnums.Types.Contains(enumType)) continue;

                var column = property.GetColumnName();
                entity.AddCheckConstraint($"ck_{table}_{column}", InList(column, SnakeCaseEnums.StoredValues(enumType)));
            }
        }
    }

    static string InList(string column, IEnumerable<string> values) =>
        $"{column} IN ({string.Join(", ", values.Select(value => $"'{value}'"))})";
}
