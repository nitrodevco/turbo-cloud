using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// <c>users</c> to <c>players</c>, with what is one row a player besides: the wallet (credits and
/// <c>users_currency</c>), Habbo Club, and the permission group of the player's rank. Ids are kept.
/// <para>
/// Passwords, e-mail addresses and SSO tickets are not copied: Turbo's site signs players in
/// itself, and a CMS that kept Morningstar's accounts writes its tickets to
/// <c>security_tickets</c> for the same player id. A name the target would see as a second copy
/// of another (the same in any case) gets the player's id appended.
/// </para>
/// </summary>
internal sealed class PlayersStep : IImportStep
{
    private const string DEFAULT_FIGURE =
        "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64";
    private const int FIGURE_MAX_LENGTH = 279;
    private const int MOTTO_MAX_LENGTH = 127;

    public string Name => "players";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        ctx.UserSettings = await UserSettings.ReadAsync(ctx.Source, ct).ConfigureAwait(false);

        var rankGroups = await ResolveRanksAsync(ctx, ct).ConfigureAwait(false);
        var memberships = await ReadClubAsync(ctx, ct).ConfigureAwait(false);
        var credits = ctx.CurrencyTypes[ImportContext.CREDITS];

        var players = ctx.Batch<PlayerEntity>("players");
        var wallets = ctx.Batch<PlayerCurrencyEntity>("player_currencies").After(players);
        var clubs = ctx.Batch<PlayerSubscriptionEntity>("player_subscriptions").After(players);
        var groups = ctx.Batch<PlayerPermissionGroupEntity>("player_permission_groups")
            .After(players);

        var names = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var row in ctx.Source.ReadAsync("users", "id", ct).ConfigureAwait(false))
        {
            var id = row.Int("id");

            if (id <= 0)
                continue;

            var name = row.Text("username").Trim();

            if (name.Length == 0)
            {
                ctx.Report.Note("player left out: no username", $"user {id}");

                continue;
            }

            if (!names.Add(PlayerNames.Key(name)))
            {
                var renamed = $"{name}_{id}";

                for (var n = 2; names.Contains(PlayerNames.Key(renamed)); n++)
                    renamed = $"{name}_{id}_{n}";

                ctx.Report.Note(
                    "player renamed: another player has the same name in another case",
                    $"user {id} '{name}' is now '{renamed}'"
                );
                name = renamed;
                names.Add(PlayerNames.Key(name));
            }

            var settings = ctx.UserSettings.GetValueOrDefault(id) ?? new UserSettings();

            // HabboInfo keeps the home room on users; older rows have it on users_settings.
            if (row.Int("home_room") > 0)
                ctx.UserSettings[id] = settings = settings with { HomeRoom = row.Int("home_room") };
            var created = row.UnixTime("account_created") ?? ctx.Now;
            var figure = row.Text("look").Trim();

            if (figure.Length == 0)
                figure = DEFAULT_FIGURE;
            else if (figure.Length > FIGURE_MAX_LENGTH)
            {
                ctx.Report.Note("player's figure cut to 279 characters", $"user {id}");
                figure = figure[..FIGURE_MAX_LENGTH];
            }

            var motto = row.String("motto")?.Trim();

            if (motto is { Length: > MOTTO_MAX_LENGTH })
                motto = motto[..MOTTO_MAX_LENGTH];

            await players
                .AddAsync(
                    new PlayerEntity
                    {
                        Id = id,
                        CreatedAt = created,
                        Name = name,
                        Motto = motto,
                        Figure = figure,
                        Gender = Gender(row.String("gender")),
                        PlayerStatus = PlayerStatusType.Offline,
                        RespectPoints = Math.Max(0, settings.RespectsReceived),
                        RespectsLeft = Math.Max(0, settings.DailyRespects),
                        PetRespectsLeft = Math.Max(0, settings.DailyPetRespects),
                        LastLoginAt = row.UnixTime("last_online") ?? row.UnixTime("last_login"),
                        OnlineMilliseconds = Math.Max(0L, settings.OnlineSeconds) * 1000L,
                    },
                    ct
                )
                .ConfigureAwait(false);

            ctx.Players.Add(id);
            ctx.PlayerNames[id] = name;

            await wallets
                .AddAsync(
                    new PlayerCurrencyEntity
                    {
                        CreatedAt = ctx.Now,
                        PlayerEntityId = id,
                        CurrencyTypeEntityId = credits,
                        Amount = Math.Max(0, row.Int("credits")),
                    },
                    ct
                )
                .ConfigureAwait(false);

            if (Club(ctx, id, created, settings, memberships.GetValueOrDefault(id)) is { } club)
                await clubs.AddAsync(club, ct).ConfigureAwait(false);

            var rank = row.Int("rank", 1);

            if (rankGroups.TryGetValue(rank, out var group))
                await groups
                    .AddAsync(
                        new PlayerPermissionGroupEntity
                        {
                            CreatedAt = ctx.Now,
                            PlayerEntityId = id,
                            GroupEntityId = group,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
        }

        await players.FlushAsync(ct).ConfigureAwait(false);

        // The other currencies, once every player is in: a row's owner must exist.
        var held = new HashSet<(int, int)>();

        await foreach (
            var row in ctx.Source.ReadAsync("users_currency", null, ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");
            var amount = row.Int("amount");

            if (!ctx.Players.Contains(player) || amount <= 0)
                continue;

            if (!ctx.CurrencyTypes.TryGetValue(row.Int("type"), out var type))
                continue;

            if (!held.Add((player, type)))
            {
                ctx.Report.Note(
                    "currency row left out: the player has another row of that currency",
                    $"user {player} type {row.Int("type")}"
                );

                continue;
            }

            await wallets
                .AddAsync(
                    new PlayerCurrencyEntity
                    {
                        CreatedAt = ctx.Now,
                        PlayerEntityId = player,
                        CurrencyTypeEntityId = type,
                        Amount = amount,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await wallets.FlushAsync(ct).ConfigureAwait(false);
        await clubs.FlushAsync(ct).ConfigureAwait(false);
        await groups.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Habbo Club, from whichever Morningstar kept: <c>users_subscriptions</c> (3.x: a start and a
    /// length in seconds a purchase) or <c>users_settings.club_expire_timestamp</c> (older). A
    /// membership that has run out is copied too, so the player keeps "has been a member".
    /// </summary>
    private static PlayerSubscriptionEntity? Club(
        ImportContext ctx,
        int player,
        DateTime created,
        UserSettings settings,
        Membership? membership
    )
    {
        var expires = Latest(membership?.Expires, settings.ClubExpires);

        if (expires is null)
            return null;

        var first = membership?.FirstStarted ?? created;
        var days = membership is { TotalSeconds: > 0 }
            ? (int)Math.Ceiling(membership.TotalSeconds / 86400d)
            : Math.Max(0, (int)Math.Ceiling((expires.Value - first).TotalDays));

        return new PlayerSubscriptionEntity
        {
            CreatedAt = ctx.Now,
            PlayerEntityId = player,
            SubscriptionType = SubscriptionType.HabboClub,
            ExpiresAt = expires,
            FirstSubscribedAt = first,
            RunStartedAt = membership?.RunStarted ?? first,
            TotalDaysSubscribed = days,
            PurchasedDaysSubscribed = days,
        };
    }

    private static DateTime? Latest(DateTime? a, DateTime? b) =>
        a is null ? b
        : b is null ? a
        : a > b ? a
        : b;

    private sealed record Membership(
        DateTime FirstStarted,
        DateTime? RunStarted,
        DateTime? Expires,
        long TotalSeconds
    );

    private static async Task<Dictionary<int, Membership>> ReadClubAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var club = new Dictionary<int, Membership>();

        await foreach (
            var row in ctx.Source.ReadAsync("users_subscriptions", "id", ct).ConfigureAwait(false)
        )
        {
            if (
                !string.Equals(
                    row.Text("subscription_type").Trim(),
                    "HABBO_CLUB",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;

            var start = row.UnixTime("timestamp_start");
            var duration = Math.Max(0L, row.Long("duration"));

            if (start is null)
                continue;

            var end = start.Value.AddSeconds(duration);
            var player = row.Int("user_id");
            var active = row.Bool("active", true);

            club[player] = club.TryGetValue(player, out var seen)
                ? new Membership(
                    seen.FirstStarted < start.Value ? seen.FirstStarted : start.Value,
                    active ? start : seen.RunStarted,
                    Latest(seen.Expires, end),
                    seen.TotalSeconds + duration
                )
                : new Membership(start.Value, active ? start : null, end, duration);
        }

        return club;
    }

    /// <summary>The permission group id of each rank that is not the default group.</summary>
    private static async Task<Dictionary<int, int>> ResolveRanksAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var groupIds = await ctx
            .Db.PermissionGroups.AsNoTracking()
            .ToDictionaryAsync(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase, ct)
            .ConfigureAwait(false);
        var names = new HashSet<string>(groupIds.Keys, StringComparer.OrdinalIgnoreCase);

        var ranks = new SortedDictionary<int, string>();

        await foreach (
            var row in ctx.Source.ReadAsync("permissions", "id", ct).ConfigureAwait(false)
        )
            ranks[row.Int("id")] = row.Text("rank_name", $"rank {row.Int("id")}");

        // A rank only the users table names (its permissions row was deleted).
        foreach (
            var rank in await ctx.Source.DistinctAsync("users", "rank", ct).ConfigureAwait(false)
        )
            if (int.TryParse(rank, out var id))
                ranks.TryAdd(id, $"rank {id}");

        var resolved = new Dictionary<int, int>();

        foreach (var (rank, rankName) in ranks)
        {
            var (group, reason) = RankGroups.Resolve(rank, rankName, ctx.Options.RankGroups, names);

            if (
                !string.Equals(
                    group,
                    PermissionGroupNames.DEFAULT,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                if (!groupIds.TryGetValue(group, out var groupId))
                    throw new ImportException(
                        $"--rank {rank}={group}: the target has no permission group '{group}'. "
                            + $"It has {string.Join(", ", groupIds.Keys.Order())}."
                    );

                resolved[rank] = groupId;
            }

            ctx.Log.LogInformation(
                "Rank {Rank} ({RankName}) -> permission group {Group} ({Reason})",
                rank,
                rankName,
                group,
                reason
            );
        }

        return resolved;
    }

    private static AvatarGenderType Gender(string? value) =>
        value?.Trim().ToUpperInvariant() is "F" or "FEMALE"
            ? AvatarGenderType.Female
            : AvatarGenderType.Male;
}
