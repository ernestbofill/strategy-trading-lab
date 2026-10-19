# Template assets

Which stocks, funds and indexes are seeded, and which ones each template watches by default: its starter list, or its one fund.

- **The database holds the assets.** The `assets` and `asset_texts` tables have the rows (symbol, exchange, name, kind, and "what it does" per language). The API reads them from there.
- **The code holds which assets each template uses.** [TemplateCatalog.cs](TemplateCatalog.cs) points at assets by their fixed id.
- **[TemplateAssets.cs](TemplateAssets.cs) is the seed.** EF generates the migration that inserts the rows from it (`HasData`).

## Use a stock that's already seeded

Edit the template's list in [TemplateCatalog.cs](TemplateCatalog.cs):

```csharp
new StarterList([Eaton, Modine, MonolithicPower, NVent, SuperMicro, Vertiv, Vicor]),
```

For a fund template, change `new DefaultFund(Spy)`.

No migration is needed, because the asset rows don't change. If the template has texts that count its stocks (`scope` and `universe` in [Texts/en.json](Texts/en.json)), update them as well.

## Remove a stock from a template

Delete it from the template's list. Keep its entry in `TemplateAssets.cs` and its row in the database, because strategies may still watch it. A delisted stock gets `active = false`; its row is never deleted.

## Add a stock that isn't seeded yet

1. In [TemplateAssets.cs](TemplateAssets.cs), add a line with the next unused number, then add it to `All`:

   ```csharp
   public static readonly Seed Apple = Stock(21, "AAPL", "XNAS", "USD", "Apple", "Phones, computers and services");
   ```

   - The number becomes the row's id (`019a0000-0000-7000-8001-000000000021`). **Never reuse or change one.** Stocks use 1 and up, funds 1001 and up, indexes 2001 and up (`Fund(...)`, `Index(...)`).
   - The symbol is the **Yahoo Finance symbol**, the price data's source (the second column of the POC's `seed.py`). Use it as Yahoo writes it, for example `BRK-B`, not `BRK.B`.
   - The exchange is the MIC code: `XNAS` (Nasdaq), `XNYS` (NYSE), `ARCX` (NYSE Arca), `XLON` (London), `XPAR` (Paris), `XAMS` (Amsterdam), `XHKG` (Hong Kong)…
   - The currency is the listing's trading currency (ISO code: `USD`, `EUR`, `GBP`…).
   - The last argument is the English "what it does": one short line, in plain words.

2. Reference it in the template's list in [TemplateCatalog.cs](TemplateCatalog.cs).

3. From `backend/`, generate the migration that inserts the row, and refresh the SQL script:

   ```bash
   dotnet ef migrations add SeedMoreAssets --project src/StrategyTradingLab.Api -o Data/Migrations
   ```

   ```bash
   dotnet ef migrations script --idempotent --project src/StrategyTradingLab.Api -o migrations.sql
   ```

4. Apply it to your Neon `dev` branch:

   ```bash
   dotnet ef database update --project src/StrategyTradingLab.Api
   ```

5. Bump `<Version>` in [Directory.Build.props](../../../Directory.Build.props). Merging to `main` deploys the change and applies the migration on the server.

## Change a seeded stock's name or text

Edit its line in `TemplateAssets.cs` and generate a migration as in step 3. EF turns the change into an update of that row.

## Checks

- `TemplateTests` fails if a template points at an asset that isn't in `TemplateAssets.All`, or at the wrong kind (a stock in a fund template, or the other way round).
- `NeonBranchTests.Every_template_asset_is_in_the_database` fails if the linked Neon branch is missing a seeded asset (a migration not applied).
