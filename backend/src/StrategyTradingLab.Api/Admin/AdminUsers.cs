using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Admin;

/// <summary>A user on the admin users page. <c>LastVisit</c>: the last time any of their sessions was used (null if none is kept).</summary>
public record AdminUser(Guid Id, string Email, UserRole Role, DateTimeOffset Joined, bool Confirmed, DateTimeOffset? LastVisit,
    int Running, int Archived);

/// <summary>Running strategies of one template, by mode.</summary>
public record AdminTemplateCount(string TemplateKey, string TemplateName, int Simulation, int Live);

/// <summary>The counts above the list: accounts, confirmed ones, and running strategies by template and mode.</summary>
public record AdminUserCounts(int Users, int Confirmed, IReadOnlyList<AdminTemplateCount> Running);

public record AdminUsers(AdminUserCounts Counts, IReadOnlyList<AdminUser> Users);

public static class AdminUsersEndpoints
{
    /// <summary><c>GET /admin/users</c>: read-only for now; blocking, deleting and invites come with ROADMAP item 24.</summary>
    public static void MapUsers(RouteGroupBuilder group)
    {
        group.MapGet("/users", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var users = await db.Users.AsNoTracking()
                .Select(u => new AdminUser(u.Id, u.Email, u.Role, u.CreatedAt, u.EmailVerifiedAt != null,
                    db.Sessions.Where(s => s.UserId == u.Id).Max(s => (DateTimeOffset?)s.LastUsedAt),
                    db.Strategies.Count(s => s.UserId == u.Id && s.Status == StrategyStatus.Running),
                    db.Strategies.Count(s => s.UserId == u.Id && s.Status == StrategyStatus.Ended)))
                .ToListAsync(cancellationToken);
            var running = await db.Strategies.AsNoTracking().Where(s => s.Status == StrategyStatus.Running)
                .GroupBy(s => new { s.TemplateKey, s.Mode })
                .Select(g => new { g.Key.TemplateKey, g.Key.Mode, Count = g.Count() })
                .ToListAsync(cancellationToken);
            // In the pick screen's order, every template listed, so a template nobody runs shows as 0.
            var byTemplate = TemplateCatalog.All
                .Select(t => new AdminTemplateCount(t.Key, TemplateTexts.For(t, TemplateTexts.DefaultLocale).Name,
                    running.Where(r => r.TemplateKey == t.Key && r.Mode == StrategyMode.Simulation).Sum(r => r.Count),
                    running.Where(r => r.TemplateKey == t.Key && r.Mode == StrategyMode.Live).Sum(r => r.Count)))
                .ToList();
            return new AdminUsers(new AdminUserCounts(users.Count, users.Count(u => u.Confirmed), byTemplate),
                [.. users.OrderByDescending(u => u.Joined)]);
        })
        .WithSummary("Every account, newest first, with its role, confirmed email, last visit and strategies; and the counts above the list.");
    }
}
