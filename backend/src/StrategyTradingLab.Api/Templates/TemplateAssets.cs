using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// The stocks, funds and indexes seeded by migration: everything the templates' starter lists and
/// default funds need, plus a few to choose from. Symbols are Yahoo Finance's.
/// Ids are fixed so a template can point at them; never reuse or change one.
/// How to add or change one: template-assets.md.
/// </summary>
public static class TemplateAssets
{
    public record Seed(Guid Id, string Symbol, string Exchange, string Currency, string Name, AssetKind Kind, string What);

    static Seed Stock(int n, string symbol, string exchange, string currency, string name, string what) =>
        new(Id(n), symbol, exchange, currency, name, AssetKind.Stock, what);

    static Seed Fund(int n, string symbol, string exchange, string currency, string name, string what) =>
        new(Id(n), symbol, exchange, currency, name, AssetKind.Fund, what);

    static Seed Index(int n, string symbol, string exchange, string currency, string name, string what) =>
        new(Id(n), symbol, exchange, currency, name, AssetKind.Index, what);

    static Guid Id(int n) => new($"019a0000-0000-7000-8001-{n:D12}");

    // Stocks: 1 and up.
    public static readonly Seed Eaton = Stock(1, "ETN", "XNYS", "USD", "Eaton", "Electrical power equipment");
    public static readonly Seed Modine = Stock(2, "MOD", "XNYS", "USD", "Modine", "Cooling for data centres and vehicles");
    public static readonly Seed MonolithicPower = Stock(3, "MPWR", "XNAS", "USD", "Monolithic Power Systems", "Power chips for computers and cars");
    public static readonly Seed NVent = Stock(4, "NVT", "XNYS", "USD", "nVent Electric", "Electrical enclosures and cooling");
    public static readonly Seed SuperMicro = Stock(5, "SMCI", "XNAS", "USD", "Super Micro Computer", "Servers for AI and data centres");
    public static readonly Seed Vertiv = Stock(6, "VRT", "XNYS", "USD", "Vertiv", "Power and cooling for data centres");
    public static readonly Seed Vicor = Stock(7, "VICR", "XNAS", "USD", "Vicor", "Power modules for servers and electronics");
    public static readonly Seed Moderna = Stock(8, "MRNA", "XNAS", "USD", "Moderna", "mRNA vaccines and medicines");
    public static readonly Seed Palantir = Stock(9, "PLTR", "XNAS", "USD", "Palantir Technologies", "Data analysis software");
    public static readonly Seed Salesforce = Stock(10, "CRM", "XNYS", "USD", "Salesforce", "Customer management software");
    public static readonly Seed ServiceNow = Stock(11, "NOW", "XNYS", "USD", "ServiceNow", "Workflow software for companies");
    public static readonly Seed Snowflake = Stock(12, "SNOW", "XNYS", "USD", "Snowflake", "Cloud data platform");
    public static readonly Seed Tesla = Stock(13, "TSLA", "XNAS", "USD", "Tesla", "Electric cars and batteries");
    public static readonly Seed ZetaGlobal = Stock(14, "ZETA", "XNYS", "USD", "Zeta Global", "Marketing software and data");
    public static readonly Seed Zscaler = Stock(15, "ZS", "XNAS", "USD", "Zscaler", "Cloud security software");
    public static readonly Seed Coherent = Stock(16, "COHR", "XNYS", "USD", "Coherent", "Lasers and optical parts for networks");
    public static readonly Seed Schneider = Stock(17, "SU.PA", "XPAR", "EUR", "Schneider Electric", "Electrical power and automation equipment");
    public static readonly Seed Byd = Stock(18, "1211.HK", "XHKG", "HKD", "BYD", "Electric cars and batteries");
    public static readonly Seed MoltenVentures = Stock(19, "GROW.L", "XLON", "GBP", "Molten Ventures", "Invests in young tech companies");
    public static readonly Seed CBrain = Stock(20, "CBRAIN.CO", "XCSE", "DKK", "cBrain", "Software for governments");

    // Funds: 1001 and up.
    public static readonly Seed Spy = Fund(1001, "SPY", "ARCX", "USD", "SPDR S&P 500 ETF", "Tracks the 500 largest US companies");
    public static readonly Seed Cspx = Fund(1002, "CSPX.L", "XLON", "USD", "iShares Core S&P 500 UCITS ETF", "Tracks the 500 largest US companies");
    public static readonly Seed Iwda = Fund(1003, "IWDA.AS", "XAMS", "EUR", "iShares Core MSCI World UCITS ETF", "Large companies in developed countries");
    public static readonly Seed Qqq = Fund(1004, "QQQ", "XNAS", "USD", "Invesco QQQ Trust", "Tracks the Nasdaq-100");
    public static readonly Seed Ewy = Fund(1005, "EWY", "ARCX", "USD", "iShares MSCI South Korea ETF", "Large South Korean companies");
    public static readonly Seed Mchi = Fund(1006, "MCHI", "XNAS", "USD", "iShares MSCI China ETF", "Large Chinese companies");

    // Indexes: 2001 and up. Followed, not bought.
    public static readonly Seed Nasdaq100 = Index(2001, "^NDX", "XNAS", "USD", "Nasdaq-100", "The 100 largest non-financial Nasdaq companies");
    public static readonly Seed Dax = Index(2002, "^GDAXI", "XETR", "EUR", "DAX", "The 40 largest German companies");
    public static readonly Seed Nikkei = Index(2003, "^N225", "XJPX", "JPY", "Nikkei 225", "225 large Japanese companies");

    public static readonly Seed[] All =
    [
        Eaton, Modine, MonolithicPower, NVent, SuperMicro, Vertiv, Vicor, Moderna, Palantir, Salesforce,
        ServiceNow, Snowflake, Tesla, ZetaGlobal, Zscaler, Coherent, Schneider, Byd, MoltenVentures, CBrain,
        Spy, Cspx, Iwda, Qqq, Ewy, Mchi,
        Nasdaq100, Dax, Nikkei,
    ];

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>().HasData(All.Select(seed => new Asset
        {
            Id = seed.Id,
            Symbol = seed.Symbol,
            Exchange = seed.Exchange,
            Name = seed.Name,
            Kind = seed.Kind,
            Currency = seed.Currency,
            CreatedAt = DevSeed.SeededAt,
        }));
        modelBuilder.Entity<AssetText>().HasData(All.Select(seed => new AssetText
        {
            AssetId = seed.Id,
            Locale = TemplateTexts.DefaultLocale,
            What = seed.What,
            CreatedAt = DevSeed.SeededAt,
        }));
    }
}
