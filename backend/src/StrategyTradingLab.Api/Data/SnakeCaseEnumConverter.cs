using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StrategyTradingLab.Api.Data;

/// <summary>Stores an enum as snake_case text: <c>PriceRanAway</c> ↔ <c>price_ran_away</c>.</summary>
public class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => Names[value],
    text => Values[text])
    where TEnum : struct, Enum
{
    static readonly Dictionary<TEnum, string> Names =
        Enum.GetValues<TEnum>().ToDictionary(
            value => value, value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));

    static readonly Dictionary<string, TEnum> Values =
        Names.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>Every stored value, for the column's CHECK constraint.</summary>
    public static IEnumerable<string> StoredValues => Names.Values;
}
