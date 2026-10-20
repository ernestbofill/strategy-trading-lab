using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Data;

/// <summary>
/// The one user every request runs as until real auth exists: Ernest, who holds the strategies he starts.
/// His password is "stl", for the log-in that doesn't exist yet. Change it before there is a way to log in.
/// </summary>
public static class DevSeed
{
    public static readonly Guid DevUserId = new("019a0000-0000-7000-8000-000000000001");

    public const string DevUserEmail = "ernest@strategytradinglab.local";

    /// <summary>ASP.NET Core Identity's PasswordHasher (PBKDF2) hash of "stl"; a test checks it verifies.</summary>
    public const string DevUserPasswordHash =
        "AQAAAAIAAYagAAAAEEkw/SFwEfBT+xCwwf3uaFOLXNoGjm9pJWiDNunC/TnZTcSPl8IDVQolTo5td97WGA==";

    /// <summary>created_at of every seeded row.</summary>
    public static readonly DateTimeOffset SeededAt = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = DevUserId,
            Email = DevUserEmail,
            PasswordHash = DevUserPasswordHash,
            TimeZone = "Europe/Madrid",
            Locale = "en",
            DisclaimerAcceptedAt = SeededAt,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt,
        });
}
