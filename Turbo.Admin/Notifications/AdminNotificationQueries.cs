using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Admin.Notifications;

/// <summary>
/// Which kinds of notification a staff member may be shown: bans with <c>admin.players.view</c>,
/// refused commands with <c>admin.commandlog.view</c>. Maintenance and shutdowns are everyone's.
/// </summary>
public sealed record NotificationScope(bool Bans, bool RefusedCommands);

/// <summary>
/// The panel's bell: what staff should know of lately. Maintenance or a shutdown, coming or under
/// way; the bans of the last few days (<see cref="AdminConfig.NotificationDays"/>), who issued
/// each and why; and the commands refused for want of a permission or room rights, which is
/// someone trying what they may not. Read-only, off the rows the hotel already keeps.
/// </summary>
public sealed class AdminNotificationQueries(
    IDbContextFactory<TurboDbContext> database,
    IHotelAvailability availability,
    IOptions<AdminConfig> config,
    TimeProvider timeProvider
)
{
    private const string UNTIL_FORMAT = "yyyy-MM-dd HH':'mm";

    /// <summary>The outcomes of a command someone was not allowed to run.</summary>
    private static readonly string[] REFUSED =
    [
        CommandTelemetry.Name(CommandOutcome.Refused),
        CommandTelemetry.Name(CommandOutcome.RoomLevel),
    ];

    public async Task<NotificationsResponse> GetAsync(NotificationScope scope, CancellationToken ct)
    {
        var since = timeProvider.GetUtcNow().UtcDateTime.AddDays(-config.Value.NotificationDays);
        var take = Math.Max(1, config.Value.NotificationsPerKind);
        var items = new List<NotificationItem>();
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);

        await using var dbScope = db.ConfigureAwait(false);

        if (Availability() is { } notice)
            items.Add(notice);

        if (scope.Bans)
        {
            var bans = await db
                .PlayerSanctions.AsNoTracking()
                .Where(x => x.Kind == SanctionKind.Ban && x.CreatedAt >= since)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .Select(x => new
                {
                    x.Id,
                    x.PlayerEntityId,
                    x.IssuerEntityId,
                    x.Reason,
                    x.CreatedAt,
                    x.ExpiresAt,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var names = await NamesAsync(
                    db,
                    bans.SelectMany(x => new[] { x.PlayerEntityId, x.IssuerEntityId ?? 0 }),
                    ct
                )
                .ConfigureAwait(false);

            items.AddRange(
                bans.Select(x => new NotificationItem(
                    $"ban:{x.Id}",
                    "ban",
                    x.CreatedAt,
                    $"{Name(names, x.IssuerEntityId, "The server")} banned {Name(names, x.PlayerEntityId, $"player #{x.PlayerEntityId}")}",
                    x.ExpiresAt is { } until
                        ? $"{x.Reason} · until {until.ToString(UNTIL_FORMAT, CultureInfo.InvariantCulture)} UTC"
                        : $"{x.Reason} · permanent",
                    x.PlayerEntityId
                ))
            );
        }

        if (scope.RefusedCommands)
        {
            var refused = await db
                .CommandLogs.AsNoTracking()
                .Where(x => REFUSED.Contains(x.Outcome) && x.CreatedAt >= since)
                .OrderByDescending(x => x.Id)
                .Take(take)
                .Select(x => new
                {
                    x.Id,
                    x.PlayerEntityId,
                    x.Command,
                    x.Arguments,
                    x.CreatedAt,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var names = await NamesAsync(db, refused.Select(x => x.PlayerEntityId), ct)
                .ConfigureAwait(false);

            items.AddRange(
                refused.Select(x => new NotificationItem(
                    $"refusedCommand:{x.Id}",
                    "refusedCommand",
                    x.CreatedAt,
                    $"{Name(names, x.PlayerEntityId, "The console")} was refused :{x.Command}",
                    x.Arguments.Length > 0 ? $":{x.Command} {x.Arguments}" : null,
                    x.PlayerEntityId == 0 ? null : x.PlayerEntityId
                ))
            );
        }

        // Maintenance first, being what affects everyone; the rest newest first.
        return new NotificationsResponse([
            .. items
                .OrderByDescending(x => x.Kind == "availability")
                .ThenByDescending(x => x.AtUtc),
        ]);
    }

    /// <summary>Maintenance or a shutdown, coming or under way; none while the hotel is open.</summary>
    private NotificationItem? Availability()
    {
        var current = availability.Current;
        var title = current.Phase switch
        {
            HotelAvailabilityPhase.MaintenanceScheduled => "Maintenance is scheduled",
            HotelAvailabilityPhase.Maintenance => "The hotel is in maintenance",
            HotelAvailabilityPhase.ShutdownScheduled => "A shutdown is scheduled",
            HotelAvailabilityPhase.ShuttingDown => "The hotel is shutting down",
            _ => null,
        };

        if (title is null)
            return null;

        var at = current.AtUtc ?? timeProvider.GetUtcNow().UtcDateTime;

        return new NotificationItem(
            $"availability:{current.Phase}:{at.Ticks}",
            "availability",
            at,
            title,
            string.IsNullOrWhiteSpace(current.Reason) ? null : current.Reason,
            null
        );
    }

    private static async Task<Dictionary<int, string>> NamesAsync(
        TurboDbContext db,
        IEnumerable<int> ids,
        CancellationToken ct
    )
    {
        var wanted = ids.Where(x => x > 0).Distinct().ToArray();

        if (wanted.Length == 0)
            return [];

        return await db
            .Players.AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);
    }

    private static string Name(Dictionary<int, string> names, int? id, string otherwise) =>
        id is { } known && names.TryGetValue(known, out var name) ? name : otherwise;
}
