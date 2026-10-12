using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Extensions;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Admin.Players;

/// <summary>What of a player's history a staff member may see in their timeline.</summary>
[Flags]
public enum TimelineKinds
{
    None = 0,

    /// <summary>Moderation done to them, or by them.</summary>
    Moderation = 1,

    /// <summary>When they logged in and out.</summary>
    Logins = 2,

    /// <summary>Every change to their balances.</summary>
    Wallet = 4,

    /// <summary>Furni they gave or got from another player.</summary>
    Furni = 8,

    /// <summary>Rooms they entered.</summary>
    Rooms = 16,

    /// <summary>Commands they ran (the command log).</summary>
    Commands = 32,

    /// <summary>What they said in rooms (the chat log).</summary>
    Chat = 64,
}

/// <summary>
/// A player's history for staff, read from the records the hotel already keeps: the moderation
/// history, logins, the currency ledger, furni transfers, room entries, the command log and the
/// chat log. Each source is read newest first through its own (player, id) or (player, time)
/// index, a page at a time, and the pages merged by time, so a page costs the same for a player
/// with ten rows as for one with ten million. Every list is newest first and paged by a cursor
/// (the last id or time seen), never by an offset, which would read every row it skips.
/// </summary>
public sealed class AdminPlayerAudit(IDbContextFactory<TurboDbContext> database)
{
    public const int PAGE_SIZE = 50;

    /// <summary>
    /// Moderation actions, newest first, narrowed to a player they were done to, a staff member
    /// who did them, a room, or a kind; any mix.
    /// </summary>
    public async Task<ModerationActionsResponse> ModerationAsync(
        int? target,
        int? actor,
        int? room,
        string? action,
        long? before,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var scope = db.ConfigureAwait(false);

        var rows = db.ModerationActions.AsNoTracking();

        if (target is { } t)
            rows = rows.Where(x => x.TargetPlayerId == t);

        if (actor is { } a)
            rows = rows.Where(x => x.ActorId == a);

        if (room is { } r)
            rows = rows.Where(x => x.RoomId == r);

        if (
            !string.IsNullOrWhiteSpace(action)
            && Enum.TryParse<ModerationActionType>(action.Trim(), true, out var kind)
            && Enum.IsDefined(kind)
        )
            rows = rows.Where(x => x.Action == kind);
        else if (!string.IsNullOrWhiteSpace(action))
            return new ModerationActionsResponse([], null);

        if (before is { } older)
            rows = rows.Where(x => x.Id < older);

        var page = await rows.OrderByDescending(x => x.Id)
            .Take(PAGE_SIZE + 1)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var more = page.Count > PAGE_SIZE;

        if (more)
            page.RemoveAt(PAGE_SIZE);

        var players = await NamesAsync(
                db,
                page.SelectMany(x => new[] { x.ActorId, x.TargetPlayerId }),
                ct
            )
            .ConfigureAwait(false);
        var rooms = await RoomNamesAsync(db, page.Select(x => x.RoomId), ct).ConfigureAwait(false);

        return new ModerationActionsResponse(
            [.. page.Select(x => Entry(x, players, rooms))],
            more ? page[^1].Id : null
        );
    }

    /// <summary>
    /// The player's history, newest first, from the sources <paramref name="kinds"/> allows,
    /// before <paramref name="before"/> when it is given.
    /// </summary>
    public async Task<PlayerTimelineResponse> TimelineAsync(
        int playerId,
        TimelineKinds kinds,
        DateTime? before,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var scope = db.ConfigureAwait(false);

        var until = before ?? DateTime.MaxValue;
        var events = new List<PlayerTimelineEvent>();
        var names = new NameBook(db);

        if (kinds.HasFlag(TimelineKinds.Moderation))
        {
            var done = await db
                .ModerationActions.AsNoTracking()
                .Where(x => x.TargetPlayerId == playerId && x.CreatedAt < until)
                .OrderByDescending(x => x.Id)
                .Take(PAGE_SIZE)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var did = await db
                .ModerationActions.AsNoTracking()
                .Where(x => x.ActorId == playerId && x.CreatedAt < until)
                .OrderByDescending(x => x.Id)
                .Take(PAGE_SIZE)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            names.Players(done.Select(x => x.ActorId));
            names.Players(did.Select(x => x.TargetPlayerId));
            names.Rooms(done.Concat(did).Select(x => x.RoomId));
            await names.LoadAsync(ct).ConfigureAwait(false);

            events.AddRange(
                done.Select(x => new PlayerTimelineEvent(
                    x.CreatedAt,
                    "moderation",
                    $"{x.Action} by {By(x, names)}{Until(x.ExpiresAt)}{Because(x.Reason)}{About(x.Detail)}{In(x.RoomId, names)}",
                    x.RoomId,
                    x.ActorId
                ))
            );
            events.AddRange(
                did.Where(x => x.TargetPlayerId != playerId)
                    .Select(x => new PlayerTimelineEvent(
                        x.CreatedAt,
                        "moderation",
                        $"Did {x.Action}{To(x.TargetPlayerId, names)}{Until(x.ExpiresAt)}{Because(x.Reason)}{About(x.Detail)}{In(x.RoomId, names)}",
                        x.RoomId,
                        x.TargetPlayerId
                    ))
            );
        }

        if (kinds.HasFlag(TimelineKinds.Logins))
        {
            var logins = await db
                .PlayerLogins.AsNoTracking()
                .Where(x => x.PlayerEntityId == playerId && x.CreatedAt < until)
                .OrderByDescending(x => x.Id)
                .Take(PAGE_SIZE)
                .Select(x => new { x.CreatedAt, x.LoggedOutAt })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            events.AddRange(
                logins.Select(x => new PlayerTimelineEvent(
                    x.CreatedAt,
                    "login",
                    x.LoggedOutAt is { } outAt
                        ? $"Logged in, for {Span(outAt - x.CreatedAt)}"
                        : "Logged in"
                ))
            );
        }

        if (kinds.HasFlag(TimelineKinds.Wallet))
        {
            var changes = await db
                .PlayerCurrencyLedger.AsNoTracking()
                .Where(x => x.PlayerEntityId == playerId && x.CreatedAt < until)
                .OrderByDescending(x => x.Id)
                .Take(PAGE_SIZE)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var currencyIds = changes.Select(x => x.CurrencyTypeId).Distinct().ToList();
            var currencies = await db
                .CurrencyTypes.AsNoTracking()
                .Where(x => currencyIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
                .ConfigureAwait(false);

            events.AddRange(
                changes.Select(x => new PlayerTimelineEvent(
                    x.CreatedAt,
                    "wallet",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{x.Change:+#;-#;0} {currencies.GetValueOrDefault(x.CurrencyTypeId, "currency " + x.CurrencyTypeId)} ({x.Reason}{About(x.Detail)}), balance {x.BalanceAfter}"
                    )
                ))
            );
        }

        if (kinds.HasFlag(TimelineKinds.Furni))
        {
            var transfers = await db
                .FurnitureTransfers.AsNoTracking()
                .Where(x =>
                    (x.FromPlayerId == playerId || x.ToPlayerId == playerId) && x.CreatedAt < until
                )
                .OrderByDescending(x => x.Id)
                .Take(PAGE_SIZE)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var definitionIds = transfers.Select(x => x.DefinitionId).Distinct().ToList();
            var definitions = await db
                .FurnitureDefinitions.AsNoTracking()
                .Where(x => definitionIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
                .ConfigureAwait(false);

            names.Players(
                transfers.Select(x =>
                    (int?)(x.FromPlayerId == playerId ? x.ToPlayerId : x.FromPlayerId)
                )
            );
            await names.LoadAsync(ct).ConfigureAwait(false);

            events.AddRange(
                transfers.Select(x =>
                {
                    var other = x.FromPlayerId == playerId ? x.ToPlayerId : x.FromPlayerId;
                    var what =
                        $"{definitions.GetValueOrDefault(x.DefinitionId, "furni")} #{x.FurnitureId.ToString(CultureInfo.InvariantCulture)}";

                    return new PlayerTimelineEvent(
                        x.CreatedAt,
                        "furni",
                        x.FromPlayerId == playerId
                            ? $"Gave {what} to {names.Player(other)} ({x.Reason})"
                            : $"Got {what} from {names.Player(other)} ({x.Reason})",
                        OtherPlayerId: other
                    );
                })
            );
        }

        if (kinds.HasFlag(TimelineKinds.Rooms))
        {
            var entries = await db
                .RoomEntryLogs.AsNoTracking()
                .Where(x => x.PlayerEntityId == playerId && x.CreatedAt < until)
                .OrderByDescending(x => x.CreatedAt)
                .Take(PAGE_SIZE)
                .Select(x => new { x.CreatedAt, x.RoomEntityId })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            names.Rooms(entries.Select(x => (int?)x.RoomEntityId));
            await names.LoadAsync(ct).ConfigureAwait(false);

            events.AddRange(
                entries.Select(x => new PlayerTimelineEvent(
                    x.CreatedAt,
                    "room",
                    $"Entered {names.Room(x.RoomEntityId)}",
                    x.RoomEntityId
                ))
            );
        }

        if (kinds.HasFlag(TimelineKinds.Commands))
        {
            var commands = await db
                .CommandLogs.AsNoTracking()
                .Where(x => x.PlayerEntityId == playerId && x.CreatedAt < until)
                .OrderByDescending(x => x.Id)
                .Take(PAGE_SIZE)
                .Select(x => new
                {
                    x.CreatedAt,
                    x.Command,
                    x.Arguments,
                    x.Outcome,
                    x.RoomEntityId,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            events.AddRange(
                commands.Select(x => new PlayerTimelineEvent(
                    x.CreatedAt,
                    "command",
                    $":{x.Command} {x.Arguments} ({x.Outcome})".Replace(
                        "  ",
                        " ",
                        StringComparison.Ordinal
                    ),
                    x.RoomEntityId
                ))
            );
        }

        if (kinds.HasFlag(TimelineKinds.Chat))
        {
            var lines = await db
                .Chatlogs.AsNoTracking()
                .Where(x => x.PlayerEntityId == playerId && x.CreatedAt < until)
                .OrderByDescending(x => x.Id)
                .Take(PAGE_SIZE)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            names.Players(lines.Select(x => x.TargetPlayerEntityId));
            names.Rooms(lines.Select(x => (int?)x.RoomEntityId));
            await names.LoadAsync(ct).ConfigureAwait(false);

            events.AddRange(
                lines.Select(x => new PlayerTimelineEvent(
                    x.CreatedAt,
                    "chat",
                    (
                        x.TargetPlayerEntityId is { } to
                            ? $"Whispered to {names.Player(to)}: \"{x.Message}\""
                            : $"Said \"{x.Message}\""
                    )
                        + (x.TypedMessage is { } typed ? $" (typed \"{typed}\")" : string.Empty)
                        + $" in {names.Room(x.RoomEntityId)}",
                    x.RoomEntityId,
                    x.TargetPlayerEntityId
                ))
            );
        }

        // Each source gave its own newest page, so the newest page of them all is among these.
        var page = events.OrderByDescending(x => x.AtUtc).Take(PAGE_SIZE).ToList();

        return new PlayerTimelineResponse(page, page.Count == PAGE_SIZE ? page[^1].AtUtc : null);
    }

    /// <summary>The player's logins, newest first; addresses and machines only when asked for.</summary>
    public async Task<IReadOnlyList<PlayerLoginEntry>> LoginsAsync(
        int playerId,
        bool identity,
        DateTime? before,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var scope = db.ConfigureAwait(false);

        var until = before ?? DateTime.MaxValue;
        var rows = await db
            .PlayerLogins.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId && x.CreatedAt < until)
            .OrderByDescending(x => x.Id)
            .Take(PAGE_SIZE)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(x => new PlayerLoginEntry(
                x.CreatedAt,
                x.LoggedOutAt,
                identity ? x.IpAddress?.ToString() : null,
                identity ? x.MachineId : null
            )),
        ];
    }

    /// <summary>
    /// Other accounts that logged in from an address or a machine this player did, within what
    /// the hotel keeps of logins. A machine shared is listed first: an address can be a school's
    /// or a phone network's, shared by strangers.
    /// </summary>
    public async Task<IReadOnlyList<AltAccount>> AltsAsync(int playerId, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var scope = db.ConfigureAwait(false);

        var alts = await db.FindAltsAsync(playerId, PAGE_SIZE, ct).ConfigureAwait(false);

        var ids = alts.Select(x => x.PlayerId).ToList();
        var names = await db
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var banned = (
            await db
                .PlayerSanctions.AsNoTracking()
                .Where(x =>
                    ids.Contains(x.PlayerEntityId)
                    && x.Kind == SanctionKind.Ban
                    && x.RevokedAt == null
                    && (x.ExpiresAt == null || x.ExpiresAt > now)
                )
                .Select(x => x.PlayerEntityId)
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToHashSet();

        return
        [
            .. alts.Select(x => new AltAccount(
                x.PlayerId,
                names.GetValueOrDefault(x.PlayerId),
                x.SharedAddresses,
                x.SharedMachine,
                x.LastSharedAt,
                banned.Contains(x.PlayerId)
            )),
        ];
    }

    private static ModerationActionEntry Entry(
        ModerationActionEntity x,
        Dictionary<int, string> players,
        Dictionary<int, string> rooms
    ) =>
        new(
            x.Id,
            x.CreatedAt,
            x.Action.ToString(),
            x.Source.ToString(),
            x.ActorId,
            x.ActorId is { } a ? players.GetValueOrDefault(a) : null,
            x.TargetPlayerId,
            x.TargetPlayerId is { } t ? players.GetValueOrDefault(t) : null,
            x.RoomId,
            x.RoomId is { } r ? rooms.GetValueOrDefault(r) : null,
            x.GuildId,
            x.Reason,
            x.ExpiresAt,
            x.Detail
        );

    private static async Task<Dictionary<int, string>> NamesAsync(
        TurboDbContext db,
        IEnumerable<int?> ids,
        CancellationToken ct
    )
    {
        var wanted = ids.OfType<int>().Distinct().ToList();

        return await db
            .Players.AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);
    }

    private static async Task<Dictionary<int, string>> RoomNamesAsync(
        TurboDbContext db,
        IEnumerable<int?> ids,
        CancellationToken ct
    )
    {
        var wanted = ids.OfType<int>().Distinct().ToList();

        return await db
            .Rooms.AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);
    }

    private static string By(ModerationActionEntity x, NameBook names) =>
        x.ActorId is { } actor ? names.Player(actor)
        : x.Source == ModerationSource.Console ? "the console"
        : "the hotel";

    private static string To(int? target, NameBook names) =>
        target is { } t ? $" to {names.Player(t)}" : string.Empty;

    private static string In(int? room, NameBook names) =>
        room is { } r ? $" in {names.Room(r)}" : string.Empty;

    private static string Until(DateTime? expires) =>
        expires is { } at
            ? string.Create(CultureInfo.InvariantCulture, $" until {at:yyyy-MM-dd HH:mm} UTC")
            : string.Empty;

    private static string Because(string? reason) =>
        string.IsNullOrEmpty(reason) ? string.Empty : $": {reason}";

    private static string About(string? detail) =>
        string.IsNullOrEmpty(detail) ? string.Empty : $" [{detail}]";

    private static string Span(TimeSpan span) =>
        span.TotalHours >= 1
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)span.TotalHours} h {span.Minutes} min"
            )
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{Math.Max(0, (int)span.TotalMinutes)} min"
            );

    /// <summary>The names of the players and rooms a page mentions, read once per source.</summary>
    private sealed class NameBook(TurboDbContext db)
    {
        private readonly Dictionary<int, string> _players = [];
        private readonly Dictionary<int, string> _rooms = [];
        private readonly HashSet<int> _wantedPlayers = [];
        private readonly HashSet<int> _wantedRooms = [];

        public void Players(IEnumerable<int?> ids)
        {
            foreach (var id in ids.OfType<int>())
                if (!_players.ContainsKey(id))
                    _wantedPlayers.Add(id);
        }

        public void Rooms(IEnumerable<int?> ids)
        {
            foreach (var id in ids.OfType<int>())
                if (!_rooms.ContainsKey(id))
                    _wantedRooms.Add(id);
        }

        public async Task LoadAsync(CancellationToken ct)
        {
            if (_wantedPlayers.Count > 0)
            {
                foreach (
                    var (id, name) in await NamesAsync(db, _wantedPlayers.Select(x => (int?)x), ct)
                        .ConfigureAwait(false)
                )
                    _players[id] = name;

                _wantedPlayers.Clear();
            }

            if (_wantedRooms.Count > 0)
            {
                foreach (
                    var (id, name) in await RoomNamesAsync(
                            db,
                            _wantedRooms.Select(x => (int?)x),
                            ct
                        )
                        .ConfigureAwait(false)
                )
                    _rooms[id] = name;

                _wantedRooms.Clear();
            }
        }

        public string Player(int id) =>
            _players.TryGetValue(id, out var name)
                ? name
                : string.Create(CultureInfo.InvariantCulture, $"player {id}");

        public string Room(int id) =>
            _rooms.TryGetValue(id, out var name)
                ? $"\"{name}\""
                : string.Create(CultureInfo.InvariantCulture, $"room {id}");
    }
}
