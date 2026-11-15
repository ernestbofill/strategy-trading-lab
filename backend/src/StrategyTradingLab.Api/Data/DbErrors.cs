using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StrategyTradingLab.Api.Data;

public static class DbErrors
{
    /// <summary>
    /// Whether a save lost a race to a unique index (on <paramref name="constraint"/> when given): another request stored the
    /// same row first. Covers a raw <see cref="PostgresException"/> too (ExecuteSql and friends throw it unwrapped).
    /// </summary>
    public static bool IsUniqueViolation(this Exception error, string? constraint = null) =>
        (error is DbUpdateException { InnerException: PostgresException inner } ? inner : error as PostgresException) is
            { SqlState: PostgresErrorCodes.UniqueViolation } violation
        && (constraint is null || violation.ConstraintName == constraint);
}
