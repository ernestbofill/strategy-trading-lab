using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>The rules a new strategy must meet. The budget limits are the Set your budget screen's.</summary>
public static class NewStrategyValidation
{
    public const int NameMaxLength = 60;
    public const decimal MinBudget = 500, MaxBudget = 1_000_000, MinStep = 10;
    public const int MaxSteps = 20;
    /// <summary>The most stocks a strategy watches (spec: "+ Add a stock").</summary>
    public const int MaxStocks = 60;

    /// <summary>Errors by field, empty when the strategy is valid. <c>assets</c> are the ones found for <c>AssetIds</c>.</summary>
    public static Dictionary<string, string[]> Validate(NewStrategy request, Template template, IReadOnlyCollection<Asset> assets, string locale = "en")
    {
        var errors = new Dictionary<string, string[]>();
        void Fail(string field, string message) => errors[field] = [message];

        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > NameMaxLength) Fail("name", Messages.UseNameLength.In(locale, NameMaxLength));

        if (request.Mode is not { } mode || !Enum.IsDefined(mode)) Fail("mode", Messages.PickMode.In(locale));

        if (request.Currency is not null && !AccountRules.Currencies.Contains(request.Currency)) Fail("currency", Messages.PickCurrency.In(locale));

        ValidateBudget(request.Budget, template, Fail, locale);

        var fund = template.Watches is DefaultFund;
        var ids = request.AssetIds ?? [];
        if (ids.Distinct().Count() != ids.Count) Fail("assetIds", Messages.ListedOnce.In(locale));
        else if (ids.Count == 0) Fail("assetIds", fund ? Messages.PickTheFund.In(locale) : Messages.PickOneStock.In(locale));
        else if (fund && ids.Count != 1) Fail("assetIds", Messages.PickExactlyOneFund.In(locale));
        else if (ids.Count > MaxStocks) Fail("assetIds", Messages.TooManyStocks.In(locale, MaxStocks));
        else if (assets.Count != ids.Count || assets.Any(asset => !asset.Active || (asset.Kind == AssetKind.Fund) != fund))
            Fail("assetIds", fund ? Messages.NotAFund.In(locale) : Messages.NotStocks.In(locale));

        return errors;
    }

    static void ValidateBudget(Budget budget, Template template, Action<string, string> fail, string locale)
    {
        if (budget is null) { fail("budget", Messages.SetBudget.In(locale)); return; }
        if (budget.Amount is < MinBudget or > MaxBudget || HasCents(budget.Amount))
            fail("budget.amount", Messages.WholeBudget.In(locale, MinBudget, MaxBudget));

        // An all in, all out template has no steps; the others need both.
        var steps = template.Budget.Step is not null;
        if (!steps)
        {
            if (budget.Step is not null || budget.MaxSteps is not null) fail("budget", Messages.NoSteps.In(locale));
            return;
        }
        if (budget.Step is not { } step || step < MinStep || step > budget.Amount || HasCents(step))
            fail("budget.step", Messages.WholeStep.In(locale, MinStep));
        if (budget.MaxSteps is not (>= 1 and <= MaxSteps)) fail("budget.maxSteps", Messages.UseSteps.In(locale, MaxSteps));
    }

    static bool HasCents(decimal amount) => amount != decimal.Truncate(amount);
}
