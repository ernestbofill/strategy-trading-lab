using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StrategyTradingLab.Api.Data;

/// <summary>Stores an enum as snake_case text: <c>PriceRanAway</c> ↔ <c>price_ran_away</c>.</summary>
public class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => Names[value],
    text => Values[text])
    where TEnum : struct, Enum
{
    static readonly Dictionary<TEnum, string> Names =
        Enum.GetValues<TEnum>().ToDictionary(value => value, value => ToSnakeCase(value.ToString()));

    static readonly Dictionary<string, TEnum> Values =
        Names.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>Every stored value, for the column's CHECK constraint.</summary>
    public static IEnumerable<string> StoredValues => Names.Values;

    public static string ToSnakeCase(string name)
    {
        var result = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) result.Append('_');
            result.Append(char.ToLowerInvariant(name[i]));
        }
        return result.ToString();
    }
}
