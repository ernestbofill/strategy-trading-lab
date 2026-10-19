using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Data;

/// <summary>The one user every request runs as until real auth exists.</summary>
public static class DevSeed
{
    public static readonly Guid DevUserId = new("019a0000-0000-7000-8000-000000000001");

    /// <summary>created_at of every seeded row.</summary>
    public static readonly DateTimeOffset SeededAt = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = DevUserId,
            Email = "dev@strategytradinglab.local",
            TimeZone = "Europe/Madrid",
            Locale = "en",
            DisclaimerAcceptedAt = SeededAt,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt,
        });
}
