using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Permissions;
using Turbo.Import;
using Turbo.Import.Morningstar;
using Turbo.Import.Morningstar.Formats;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Import;

/// <summary>
/// A Morningstar hotel's players and what is theirs, imported into an empty Turbo database: what
/// a player logging in on Turbo would find.
/// </summary>
public class MorningstarImportPlayersTests
{
    internal static readonly DateTimeOffset NOW = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    internal static long Unix(DateTimeOffset time) => time.ToUnixTimeSeconds();

    internal static MorningstarImportOptions Options(bool dryRun = false) =>
        new()
        {
            DryRun = dryRun,
            Pets = new PetRules
            {
                LevelExperience = new PetConfig().LevelExperienceThresholds,
                MaxLevel = new PetConfig().MaxLevel,
                MonsterplantMaxLevel = new PetConfig().MonsterplantMaxLevel,
                MonsterplantGrowthSeconds = new PetConfig().MonsterplantGrowthSeconds,
                MonsterplantWellBeingSeconds = new PetConfig().MonsterplantWellBeingSeconds,
                MonsterplantBabyWellBeingSeconds = new PetConfig().MonsterplantBabyWellBeingSeconds,
            },
            LogicKeys = InteractionLogic.Registered(typeof(RoomModule).Assembly),
        };

    internal static async Task<ImportReport> ImportAsync(
        MorningstarDb source,
        SqliteDb target,
        MorningstarImportOptions? options = null
    )
    {
        var ct = TestContext.Current.CancellationToken;

        await using var db = await target.CreateDbContextAsync(ct);
        await using var reader = await source.OpenAsync(ct);

        var importer = new MorningstarImporter(
            db,
            options ?? Options(),
            NullLogger<MorningstarImporter>.Instance,
            new ManualTimeProvider(NOW)
        );

        return await importer.RunAsync(reader, ct);
    }

    /// <summary>Two players, their ranks and a little of everything a player has.</summary>
    internal static MorningstarDb Hotel()
    {
        var ms = new MorningstarDb();

        ms.Insert(
                "permissions",
                new
                {
                    id = 1,
                    rank_name = "User",
                    level = 1,
                }
            )
            .Insert(
                "permissions",
                new
                {
                    id = 7,
                    rank_name = "Administrator",
                    level = 7,
                }
            )
            .Insert(
                "users",
                new
                {
                    id = 10,
                    username = "Alice",
                    password = "$2y$10$secret",
                    mail = "alice@example.test",
                    motto = "hello",
                    look = "hr-100-61.hd-180-1",
                    gender = "F",
                    rank = 7,
                    credits = 2500,
                    account_created = Unix(NOW.AddYears(-3)),
                    last_online = Unix(NOW.AddDays(-1)),
                    auth_ticket = "ticket-alice",
                }
            )
            .Insert(
                "users",
                new
                {
                    id = 11,
                    username = "Bob",
                    look = "hr-115-42.hd-195-19",
                    gender = "M",
                    rank = 1,
                    credits = 5,
                    account_created = Unix(NOW.AddYears(-1)),
                }
            )
            // Same name as Alice in another case: a second player Turbo cannot tell apart.
            .Insert(
                "users",
                new
                {
                    id = 12,
                    username = "alice",
                    look = "",
                    gender = "M",
                    rank = 1,
                }
            )
            .Insert(
                "users_settings",
                new
                {
                    user_id = 10,
                    respects_received = 42,
                    daily_respect_points = 2,
                    daily_pet_respect_points = 3,
                    volume_system = 40,
                    volume_furni = 100,
                    volume_trax = 100,
                    block_friendrequests = "1",
                    old_chat = "1",
                    chat_color = 5,
                    club_expire_timestamp = Unix(NOW.AddDays(10)),
                    online_time = 3600,
                    guild_id = 0,
                }
            )
            .Insert(
                "users_currency",
                new
                {
                    user_id = 10,
                    type = 0,
                    amount = 300,
                }
            )
            .Insert(
                "users_currency",
                new
                {
                    user_id = 10,
                    type = 5,
                    amount = 20,
                }
            )
            .Insert(
                "users_currency",
                new
                {
                    user_id = 10,
                    type = 103,
                    amount = 7,
                }
            )
            .Insert(
                "users_subscriptions",
                new
                {
                    id = 1,
                    user_id = 10,
                    subscription_type = "HABBO_CLUB",
                    timestamp_start = Unix(NOW.AddDays(-20)),
                    duration = 31 * 86400,
                    active = 1,
                }
            )
            .Insert(
                "users_badges",
                new
                {
                    id = 1,
                    user_id = 10,
                    slot_id = 1,
                    badge_code = "ADM",
                }
            )
            .Insert(
                "users_badges",
                new
                {
                    id = 2,
                    user_id = 10,
                    slot_id = 0,
                    badge_code = "ACH_RoomEntry3",
                }
            )
            .Insert(
                "users_badges",
                new
                {
                    id = 3,
                    user_id = 99,
                    slot_id = 0,
                    badge_code = "GHOST",
                }
            )
            .Insert(
                "users_wardrobe",
                new
                {
                    user_id = 10,
                    slot_id = 1,
                    look = "hr-1.hd-2",
                    gender = "F",
                }
            )
            .Insert(
                "users_effects",
                new
                {
                    user_id = 10,
                    effect = 4,
                    duration = 0,
                    activation_timestamp = -1,
                    total = 1,
                }
            )
            .Insert(
                "users_effects",
                new
                {
                    user_id = 10,
                    effect = 12,
                    duration = 3600,
                    activation_timestamp = Unix(NOW.AddMinutes(-30)),
                    total = 3,
                }
            )
            .Insert(
                "catalog_clothing",
                new
                {
                    id = 1,
                    name = "clothing_wavy",
                    setid = "3290,3291",
                }
            )
            .Insert("users_clothing", new { user_id = 10, clothing_id = 1 })
            .Insert("users_ignored", new { user_id = 11, target_id = 10 })
            .Insert(
                "messenger_categories",
                new
                {
                    id = 3,
                    name = "Best",
                    user_id = 10,
                }
            )
            .Insert(
                "messenger_friendships",
                new
                {
                    id = 1,
                    user_one_id = 10,
                    user_two_id = 11,
                    relation = 1,
                    friends_since = Unix(NOW.AddDays(-5)),
                    category = 3,
                }
            )
            .Insert(
                "messenger_friendships",
                new
                {
                    id = 2,
                    user_one_id = 11,
                    user_two_id = 10,
                    relation = 0,
                    friends_since = Unix(NOW.AddDays(-5)),
                    category = 0,
                }
            )
            .Insert("messenger_friendrequests", new { user_to_id = 10, user_from_id = 12 })
            .Insert(
                "bans",
                new
                {
                    id = 1,
                    user_id = 11,
                    user_staff_id = 10,
                    timestamp = Unix(NOW.AddDays(-1)),
                    ban_expire = Unix(NOW.AddDays(2)),
                    ban_reason = "Spam",
                    type = "account",
                }
            )
            .Insert(
                "bans",
                new
                {
                    id = 2,
                    user_id = 12,
                    timestamp = Unix(NOW.AddDays(-9)),
                    ban_expire = Unix(NOW.AddDays(-2)),
                    ban_reason = "Old",
                    type = "account",
                }
            );

        return ms;
    }

    /// <summary>The permission groups a fresh Turbo database seeds; SqliteDb starts with none.</summary>
    internal static void SeedGroups(SqliteDb target)
    {
        foreach (
            var (id, name, weight) in new[]
            {
                (1, "default", 0),
                (2, "moderator", 50),
                (3, "admin", 100),
            }
        )
            target.Insert(
                new PermissionGroupEntity
                {
                    Id = id,
                    Name = name,
                    DisplayName = name,
                    Weight = weight,
                }
            );
    }

    [Fact]
    public async Task Players_keep_their_ids_names_looks_and_wallets()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = new SqliteDb();
        SeedGroups(target);

        var report = await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var alice = await db.Players.SingleAsync(x => x.Id == 10, ct);

        Assert.Equal("Alice", alice.Name);
        Assert.Equal("hello", alice.Motto);
        Assert.Equal("hr-100-61.hd-180-1", alice.Figure);
        Assert.Equal(AvatarGenderType.Female, alice.Gender);
        Assert.Equal(42, alice.RespectPoints);
        Assert.Equal(3_600_000, alice.OnlineMilliseconds);
        Assert.Equal(NOW.AddYears(-3).UtcDateTime, alice.CreatedAt);

        // The second "alice" keeps her account under a name Turbo can tell apart.
        Assert.Equal("alice_12", (await db.Players.SingleAsync(x => x.Id == 12, ct)).Name);
        Assert.Equal(
            1,
            report.NotesOf("player renamed: another player has the same name in another case")
        );

        var wallet = await db
            .PlayerCurrencies.Where(x => x.PlayerEntityId == 10)
            .Join(
                db.CurrencyTypes,
                w => w.CurrencyTypeEntityId,
                t => t.Id,
                (w, t) =>
                    new
                    {
                        t.CurrencyType,
                        t.ActivityPointType,
                        w.Amount,
                    }
            )
            .ToListAsync(ct);

        Assert.Contains(wallet, x => x.CurrencyType == CurrencyType.Credits && x.Amount == 2500);
        Assert.Contains(wallet, x => x.ActivityPointType == 0 && x.Amount == 300);
        Assert.Contains(wallet, x => x.ActivityPointType == 5 && x.Amount == 20);
        Assert.Contains(wallet, x => x.ActivityPointType == 103 && x.Amount == 7);
    }

    [Fact]
    public async Task Club_ranks_settings_and_collections_come_across()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = new SqliteDb();
        SeedGroups(target);

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        // The later of users_subscriptions and the legacy expiry: 11 days left from the 31.
        var club = await db.PlayerSubscriptions.SingleAsync(x => x.PlayerEntityId == 10, ct);

        Assert.Equal(SubscriptionType.HabboClub, club.SubscriptionType);
        Assert.Equal(NOW.AddDays(11).UtcDateTime, club.ExpiresAt);
        Assert.Equal(31, club.TotalDaysSubscribed);

        // Administrator by name; a plain user is in the default group, which needs no row.
        var groups = await db
            .PlayerPermissionGroups.Join(
                db.PermissionGroups,
                p => p.GroupEntityId,
                g => g.Id,
                (p, g) => new { p.PlayerEntityId, g.Name }
            )
            .ToListAsync(ct);

        Assert.Equal([(10, "admin")], groups.Select(x => (x.PlayerEntityId, x.Name)));

        var settings = await db.PlayerSettings.SingleAsync(x => x.PlayerEntityId == 10, ct);

        Assert.Equal(40, settings.GenericVolume);
        Assert.True(settings.FriendRequestsDisabled);
        Assert.Equal(5, settings.ChatStyleId);

        var badges = await db
            .PlayerBadges.Where(x => x.PlayerEntityId == 10)
            .OrderBy(x => x.BadgeCode)
            .ToListAsync(ct);

        Assert.Equal(["ACH_RoomEntry3", "ADM"], badges.Select(x => x.BadgeCode));
        Assert.Equal(1, badges.Single(x => x.BadgeCode == "ADM").SlotId);
        Assert.False(await db.PlayerBadges.AnyAsync(x => x.BadgeCode == "GHOST", ct));

        Assert.Equal("hr-1.hd-2", (await db.PlayerOutfits.SingleAsync(ct)).Figure);
        Assert.Equal(
            [3290, 3291],
            await db.PlayerFigureSets.OrderBy(x => x.SetId).Select(x => x.SetId).ToListAsync(ct)
        );

        var effects = await db.PlayerEffects.OrderBy(x => x.EffectId).ToListAsync(ct);

        Assert.True(effects[0].IsPermanent);
        Assert.Equal(NOW.AddMinutes(30).UtcDateTime, effects[1].ExpiresAt);
        Assert.Equal(2, effects[1].InactiveCount);
    }

    [Fact]
    public async Task Friends_requests_ignores_and_bans_come_across()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = new SqliteDb();
        SeedGroups(target);

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var friends = await db.MessengerFriends.OrderBy(x => x.PlayerEntityId).ToListAsync(ct);

        Assert.Equal(2, friends.Count);
        Assert.Equal(MessengerFriendRelationType.One, friends[0].RelationType);
        Assert.Equal(3, friends[0].MessengerCategoryEntityId);
        Assert.Null(friends[1].MessengerCategoryEntityId);

        var request = await db.MessengerRequests.SingleAsync(ct);

        Assert.Equal((12, 10), (request.PlayerEntityId, request.RequestedPlayerEntityId));
        Assert.Equal(
            (11, 10),
            await db
                .MessengerIgnored.Select(x =>
                    ValueTuple.Create(x.PlayerEntityId, x.IgnoredPlayerEntityId)
                )
                .SingleAsync(ct)
        );

        // Only the ban still running.
        var ban = await db.PlayerSanctions.SingleAsync(ct);

        Assert.Equal(11, ban.PlayerEntityId);
        Assert.Equal(10, ban.IssuerEntityId);
        Assert.Equal(NOW.AddDays(2).UtcDateTime, ban.ExpiresAt);
    }

    [Fact]
    public async Task A_target_with_players_is_refused_before_anything_is_written()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = new SqliteDb();
        SeedGroups(target);

        await ImportAsync(ms, target);

        var refused = await Assert.ThrowsAsync<ImportException>(() => ImportAsync(ms, target));

        Assert.Contains("fresh-start.sql", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dry_run_reports_what_it_would_write_and_writes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = new SqliteDb();
        SeedGroups(target);

        var report = await ImportAsync(ms, target, Options(dryRun: true));

        await using var db = await target.CreateDbContextAsync(ct);

        Assert.Equal(3, report.WrittenTo("players"));
        Assert.False(await db.Players.AnyAsync(ct));
        Assert.False(await db.CurrencyTypes.AnyAsync(ct));
    }
}
