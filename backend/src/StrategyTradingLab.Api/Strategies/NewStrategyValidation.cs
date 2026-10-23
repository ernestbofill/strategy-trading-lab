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

    /// <summary>Errors by field, empty when the strategy is valid. <c>assets</c> are the ones found for <c>AssetIds</c>.</summary>
    public static Dictionary<string, string[]> Validate(NewStrategy request, Template template, IReadOnlyCollection<Asset> assets)
    {
        var errors = new Dictionary<string, string[]>();
        void Fail(string field, string message) => errors[field] = [message];

        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > NameMaxLength) Fail("name", $"Use 1 to {NameMaxLength} characters.");

        if (request.Mode is not { } mode || !Enum.IsDefined(mode)) Fail("mode", "Pick simulation or live.");

        if (request.Currency is not null && !AccountRules.Currencies.Contains(request.Currency)) Fail("currency", "Pick a currency from the list.");

        ValidateBudget(request.Budget, template, Fail);

        var fund = template.Watches is DefaultFund;
        var ids = request.AssetIds ?? [];
        if (ids.Distinct().Count() != ids.Count) Fail("assetIds", "Each stock or fund can be listed once.");
        else if (ids.Count == 0) Fail("assetIds", fund ? "Pick the fund." : "Pick at least one stock.");
        else if (fund && ids.Count != 1) Fail("assetIds", "Pick exactly one fund.");
        else if (assets.Count != ids.Count || assets.Any(asset => !asset.Active || (asset.Kind == AssetKind.Fund) != fund))
            Fail("assetIds", fund ? "That is not a fund we can follow." : "Some of those are not stocks we can follow.");

        return errors;
    }

    static void ValidateBudget(Budget budget, Template template, Action<string, string> fail)
    {
        if (budget is null) { fail("budget", "Set a budget."); return; }
        if (budget.Amount is < MinBudget or > MaxBudget || HasCents(budget.Amount))
            fail("budget.amount", $"Use a whole amount from {MinBudget:0} to {MaxBudget:0}.");

        // An all in, all out template has no steps; the others need both.
        var steps = template.Budget.Step is not null;
        if (!steps)
        {
            if (budget.Step is not null || budget.MaxSteps is not null) fail("budget", "This template has no steps.");
            return;
        }
        if (budget.Step is not { } step || step < MinStep || step > budget.Amount || HasCents(step))
            fail("budget.step", $"Use a whole amount from {MinStep:0} up to the budget.");
        if (budget.MaxSteps is not (>= 1 and <= MaxSteps)) fail("budget.maxSteps", $"Use 1 to {MaxSteps} steps.");
    }

    static bool HasCents(decimal amount) => amount != decimal.Truncate(amount);
}
