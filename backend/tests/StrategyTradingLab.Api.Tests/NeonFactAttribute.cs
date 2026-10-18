using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Tests;

/// <summary>
/// A test against the linked Neon branch. Skipped when its variables aren't there (CI);
/// locally they come from the repo's .env.local (`neon env pull`).
/// </summary>
public sealed class NeonFactAttribute : FactAttribute
{
    static readonly bool Configured = LoadEnvironment();

    public NeonFactAttribute()
    {
        if (!Configured) Skip = "No Neon branch: run `neon env pull` to write .env.local.";
    }

    static bool LoadEnvironment()
    {
        NeonEnvironment.LoadDotEnv(AppContext.BaseDirectory);
        return Environment.GetEnvironmentVariable("DATABASE_URL") is not null
            && Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL_S3") is not null;
    }
}
