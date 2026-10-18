using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// The stocks and funds the templates' starter lists and default funds need, seeded by migration.
/// The lists are the POC's (trading-strategies-bot, seed.py) without its non-US listings.
/// Ids are fixed so a template can point at them; never reuse or change one.
/// </summary>
public static class TemplateAssets
{
    public record Seed(Guid Id, string Symbol, string Exchange, string Name, AssetKind Kind, string What);

    static Seed Stock(int n, string symbol, string exchange, string name, string what) =>
        new(Id(n), symbol, exchange, name, AssetKind.Stock, what);

    static Guid Id(int n) => new($"019a0000-0000-7000-8001-{n:D12}");

    // Dip in winners: the POC's power and cooling for data centres.
    public static readonly Seed Eaton = Stock(1, "ETN", "XNYS", "Eaton", "Electrical power equipment");
    public static readonly Seed Modine = Stock(2, "MOD", "XNYS", "Modine", "Cooling for data centres and vehicles");
    public static readonly Seed MonolithicPower = Stock(3, "MPWR", "XNAS", "Monolithic Power Systems", "Power chips for computers and cars");
    public static readonly Seed NVent = Stock(4, "NVT", "XNYS", "nVent Electric", "Electrical enclosures and cooling");
    public static readonly Seed SuperMicro = Stock(5, "SMCI", "XNAS", "Super Micro Computer", "Servers for AI and data centres");
    public static readonly Seed Vertiv = Stock(6, "VRT", "XNYS", "Vertiv", "Power and cooling for data centres");
    public static readonly Seed Vicor = Stock(7, "VICR", "XNAS", "Vicor", "Power modules for servers and electronics");

    // The comeback: the POC's beaten-down growth stocks.
    public static readonly Seed Moderna = Stock(8, "MRNA", "XNAS", "Moderna", "mRNA vaccines and medicines");
    public static readonly Seed Palantir = Stock(9, "PLTR", "XNAS", "Palantir Technologies", "Data analysis software");
    public static readonly Seed Salesforce = Stock(10, "CRM", "XNYS", "Salesforce", "Customer management software");
    public static readonly Seed ServiceNow = Stock(11, "NOW", "XNYS", "ServiceNow", "Workflow software for companies");
    public static readonly Seed Snowflake = Stock(12, "SNOW", "XNYS", "Snowflake", "Cloud data platform");
    public static readonly Seed Tesla = Stock(13, "TSLA", "XNAS", "Tesla", "Electric cars and batteries");
    public static readonly Seed ZetaGlobal = Stock(14, "ZETA", "XNYS", "Zeta Global", "Marketing software and data");
    public static readonly Seed Zscaler = Stock(15, "ZS", "XNAS", "Zscaler", "Cloud security software");

    // The big market drop and Ride the long trend. The POC signals on SPY.
    public static readonly Seed Spy = new(Id(1001), "SPY", "ARCX", "SPDR S&P 500 ETF", AssetKind.Fund,
        "Tracks the 500 largest US companies");

    public static readonly Seed[] All =
    [
        Eaton, Modine, MonolithicPower, NVent, SuperMicro, Vertiv, Vicor,
        Moderna, Palantir, Salesforce, ServiceNow, Snowflake, Tesla, ZetaGlobal, Zscaler,
        Spy,
    ];

    static readonly DateTimeOffset SeededAt = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>().HasData(All.Select(seed => new Asset
        {
            Id = seed.Id,
            Symbol = seed.Symbol,
            Exchange = seed.Exchange,
            Name = seed.Name,
            Kind = seed.Kind,
            Currency = "USD",
            CreatedAt = SeededAt,
        }));
        modelBuilder.Entity<AssetText>().HasData(All.Select(seed => new AssetText
        {
            AssetId = seed.Id,
            Locale = "en",
            What = seed.What,
            CreatedAt = SeededAt,
        }));
    }
}
