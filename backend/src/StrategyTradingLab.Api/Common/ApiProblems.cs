using Microsoft.AspNetCore.Http.HttpResults;

namespace StrategyTradingLab.Api.Common;

public static class ApiProblems
{
    /// <summary>A 400 naming one field and why it was refused, as the forms show it under the field.</summary>
    public static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
