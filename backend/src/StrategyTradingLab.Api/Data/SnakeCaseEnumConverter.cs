using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Data;

/// <summary>Stores an enum as snake_case text: <c>PriceRanAway</c> ↔ <c>price_ran_away</c>.</summary>
public class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => Names[value],
    text => Parse(text))
    where TEnum : struct, Enum
{
    static readonly Dictionary<TEnum, string> Names =
        Enum.GetValues<TEnum>().ToDictionary(
            value => value, value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));

    static readonly Dictionary<string, TEnum> Values =
        Names.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>Every stored value, for the column's CHECK constraint.</summary>
    public static IEnumerable<string> StoredValues => Names.Values;

    static TEnum Parse(string text) =>
        Values.TryGetValue(text, out var value)
            ? value
            : throw new InvalidOperationException(
                $"'{text}' isn't a {typeof(TEnum).Name}: the database has a value this build doesn't know.");
}

/// <summary>Which enums are stored as snake_case text: every enum in Data.Entities.</summary>
public static class SnakeCaseEnums
{
    public static readonly IReadOnlySet<Type> Types = typeof(AssetKind).Assembly.GetTypes()
        .Where(type => type.IsEnum && type.Namespace == typeof(AssetKind).Namespace)
        .ToHashSet();

    public static Type ConverterFor(Type enumType) => typeof(SnakeCaseEnumConverter<>).MakeGenericType(enumType);

    public static IEnumerable<string> StoredValues(Type enumType) =>
        (IEnumerable<string>)ConverterFor(enumType)
            .GetProperty(nameof(SnakeCaseEnumConverter<AssetKind>.StoredValues))!.GetValue(null)!;
}
