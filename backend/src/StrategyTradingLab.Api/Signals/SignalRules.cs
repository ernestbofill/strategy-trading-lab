using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Signals;

/// <summary>A trade ready to store: both the amount and the shares, whichever the user typed.</summary>
public record CheckedTrade(DateOnly Date, decimal Price, EnteredAs EnteredAs, decimal Amount, decimal Shares, decimal Fee, decimal FxRate, FxSource FxSource);

/// <summary>The rules of Confirm a signal (spec: Confirm a signal; DATA-MODEL: user_trades). Pure: no database.</summary>
public static class SignalRules
{
    public const decimal MaxAmount = 1_000_000;

    /// <summary>The next weekday after <paramref name="date"/>: the prefill's trade day when no later price is stored, and the snooze day.</summary>
    public static DateOnly NextWeekday(DateOnly date)
    {
        var next = date.AddDays(1);
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) next = next.AddDays(1);
        return next;
    }

    /// <summary>
    /// Checks "I did it" and works out the value the user didn't type (<c>shares = amount × fx ÷ price</c>). The date is from
    /// the signal day to today. A reference rate comes from <paramref name="referenceRate"/> (the date's), a user rate from the
    /// request. Errors by field, empty when the trade is valid.
    /// </summary>
    public static (CheckedTrade? Trade, Dictionary<string, string[]> Errors) CheckTrade(SignalTradeRequest request, DateOnly signalDate,
        DateOnly today, Func<DateOnly, decimal?> referenceRate)
    {
        var errors = new Dictionary<string, string[]>();
        void Fail(string field, string message) => errors[field] = [message];

        if (request.Date is not { } date) Fail("date", "Enter the day you traded.");
        else if (date < signalDate) Fail("date", "Use a day on or after the signal.");
        else if (date > today) Fail("date", "Use a day up to today.");

        if (request.Price is not (> 0 and < 1_000_000)) Fail("price", "Enter the price per share.");

        var enteredAs = request.EnteredAs ?? EnteredAs.Amount;
        if (enteredAs == EnteredAs.Amount && (request.Amount is not (> 0 and <= MaxAmount) || request.Amount != Math.Round(request.Amount.Value, 2)))
            Fail("amount", "Enter the amount you spent, without the fee.");
        if (enteredAs == EnteredAs.Shares && request.Shares is not (> 0 and < 1_000_000_000))
            Fail("shares", "Enter the number of shares.");

        var fee = request.Fee ?? 0;
        if (fee is < 0 or > MaxAmount || fee != Math.Round(fee, 2)) Fail("fee", "Enter the fee, or leave it empty.");

        var source = request.FxSource ?? FxSource.Reference;
        decimal? fx = null;
        if (source == FxSource.User)
        {
            if (request.FxRate is not (> 0 and < 1_000_000)) Fail("fxRate", "Enter the rate your broker used.");
            else fx = request.FxRate;
        }
        else if (request.Date is { } day && !errors.ContainsKey("date"))
        {
            fx = referenceRate(day);
            if (fx is null) Fail("fxRate", "There is no reference rate for that day yet: enter your broker's.");
        }

        if (errors.Count > 0 || fx is not { } rate || request.Price is not { } price) return (null, errors);

        var (amount, shares) = enteredAs == EnteredAs.Amount
            ? (request.Amount!.Value, Math.Round(request.Amount!.Value * rate / price, 6))
            : (Math.Round(request.Shares!.Value * price / rate, 2), request.Shares!.Value);
        if (amount <= 0 || shares <= 0) Fail(enteredAs == EnteredAs.Amount ? "amount" : "shares", "That is too small to be a trade.");
        else if (amount > MaxAmount) Fail("shares", "That is more than one trade can be.");
        return errors.Count > 0 ? (null, errors) : (new CheckedTrade(request.Date!.Value, price, enteredAs, amount, shares, fee, rate, source), errors);
    }
}
