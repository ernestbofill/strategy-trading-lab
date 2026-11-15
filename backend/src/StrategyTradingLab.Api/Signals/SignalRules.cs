using StrategyTradingLab.Api.Auth;
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
    /// Checks a trade (signal or manual) and works out the value the user didn't type (<c>shares = amount × fx ÷ price</c>).
    /// The date is from <paramref name="minDate"/> to today. A reference rate comes from <paramref name="referenceRate"/> (the date's),
    /// a user rate from the request. Errors by field, empty when the trade is valid, in <paramref name="locale"/> ("ca", else English).
    /// </summary>
    public static (CheckedTrade? Trade, Dictionary<string, string[]> Errors) CheckTrade(SignalTradeRequest request, DateOnly? minDate,
        DateOnly today, Func<DateOnly, decimal?> referenceRate, string? minDateMessage = null, string locale = "en")
    {
        var errors = new Dictionary<string, string[]>();
        void Fail(string field, string message) => errors[field] = [message];

        if (request.Date is not { } date) Fail("date", Messages.EnterTradeDay.In(locale));
        else if (minDate is { } min && date < min) Fail("date", minDateMessage ?? Messages.DayAfterSignal.In(locale));
        else if (date > today) Fail("date", Messages.DayUpToToday.In(locale));

        if (request.Price is not (>= 0.0001m and < 1_000_000)) Fail("price", Messages.EnterPrice.In(locale));
        else if (request.Price != Math.Round(request.Price.Value, 4)) Fail("price", Messages.UpToDecimals.In(locale, 4));

        var enteredAs = request.EnteredAs ?? EnteredAs.Amount;
        if (enteredAs == EnteredAs.Amount)
        {
            if (request.Amount is not (> 0 and <= MaxAmount) || request.Amount != Math.Round(request.Amount.Value, 2))
                Fail("amount", Messages.EnterAmount.In(locale));
        }
        else if (enteredAs == EnteredAs.Shares)
        {
            if (request.Shares is not (> 0 and < 1_000_000_000)) Fail("shares", Messages.EnterShares.In(locale));
            else if (request.Shares != Math.Round(request.Shares.Value, 6)) Fail("shares", Messages.UpToDecimals.In(locale, 6));
        }

        var fee = request.Fee ?? 0;
        if (fee is < 0 or > MaxAmount || fee != Math.Round(fee, 2)) Fail("fee", Messages.EnterFee.In(locale));

        var source = request.FxSource ?? FxSource.Reference;
        decimal? fx = null;
        if (source == FxSource.User)
        {
            if (request.FxRate is not (> 0 and < 1_000_000)) Fail("fxRate", Messages.EnterBrokerRate.In(locale));
            else if (request.FxRate != Math.Round(request.FxRate.Value, 8)) Fail("fxRate", Messages.UpToDecimals.In(locale, 8));
            else fx = request.FxRate;
        }
        else if (request.Date is { } day && !errors.ContainsKey("date"))
        {
            fx = referenceRate(day);
            if (fx is null) Fail("fxRate", Messages.NoReferenceRate.In(locale));
        }

        if (errors.Count > 0 || fx is not { } rate || request.Price is not { } price) return (null, errors);

        var (amount, shares) = enteredAs == EnteredAs.Amount
            ? (request.Amount!.Value, Math.Round(request.Amount!.Value * rate / price, 6))
            : (Math.Round(request.Shares!.Value * price / rate, 2), request.Shares!.Value);
        if (amount <= 0 || shares <= 0) Fail(enteredAs == EnteredAs.Amount ? "amount" : "shares", Messages.TradeTooSmall.In(locale));
        else if (amount > MaxAmount || shares >= 1_000_000_000) Fail(enteredAs == EnteredAs.Amount ? "amount" : "shares", Messages.TradeTooBig.In(locale));
        return errors.Count > 0 ? (null, errors) : (new CheckedTrade(request.Date!.Value, price, enteredAs, amount, shares, fee, rate, source), errors);
    }
}
