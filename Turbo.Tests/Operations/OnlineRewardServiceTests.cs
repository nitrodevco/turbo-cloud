using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Database.Configuration;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Database.Movements;
using Turbo.Operations.Configuration;
using Turbo.Operations.OnlineRewards;
using Turbo.Players.Grains;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// Currency for time online (<c>Turbo:OnlineRewards</c>): each login's clock, the Habbo Club
/// bonus, the daily cap, idle players, and nothing at all while it is off.
/// </summary>
public sealed class OnlineRewardServiceTests : IDisposable
{
    private const int ALICE = 1;
    private const int BOB = 2;
    private const int CREDITS_ID = 1;
    private const int DUCKETS_ID = 2;
    private const int ROOM = 7;

    private static readonly DateTime Login = new(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(Login));
    private readonly TestOptionsMonitor<OnlineRewardsConfig> _config = new(new());
    private readonly HashSet<long> _club = [];
    private readonly HashSet<long> _asleep = [];
    private readonly HashSet<long> _inRoom = [];
    private readonly OnlineRewardService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public OnlineRewardServiceTests()
    {
        foreach (var (id, name) in new[] { (ALICE, "alice"), (BOB, "bob") })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Online,
                }
            );
        _db.Insert(
            new CurrencyTypeEntity
            {
                Id = CREDITS_ID,
                Name = "credits",
                CurrencyType = CurrencyType.Credits,
                Enabled = true,
            }
        );
        _db.Insert(
            new CurrencyTypeEntity
            {
                Id = DUCKETS_ID,
                Name = "duckets",
                CurrencyType = CurrencyType.ActivityPoints,
                ActivityPointType = 0,
                Enabled = true,
            }
        );

        _fakes.Handlers[nameof(ICurrencyTypeProvider.TryGetCurrencyKindByName)] = call =>
        {
            CurrencyKind? kind = ((string)call.Args[0]!).ToLowerInvariant() switch
            {
                "credits" => CurrencyKind.Credits,
                "duckets" => CurrencyKind.ActivityPoints(0),
                _ => null,
            };
            call.Args[1] = kind ?? default;

            return kind is not null;
        };
        _fakes.Handlers[nameof(ICurrencyTypeProvider.TryGetCurrencyTypeId)] = call =>
        {
            call.Args[1] =
                (CurrencyKind)call.Args[0]! == CurrencyKind.Credits ? CREDITS_ID : DUCKETS_ID;

            return true;
        };
        // A fake wallet takes every credit; the real one is wired in where the client's view matters.
        _fakes.Handlers[nameof(IPlayerWalletGrain.CreditAsync)] = call =>
            call.Args.Length == 4 ? Task.FromResult(true) : Fakes.NotHandled;
        _fakes.Handlers[nameof(IPlayerSubscriptionGrain.HasActiveAsync)] = call =>
            Task.FromResult(_club.Contains((long)call.Key!));
        _fakes.Handlers[nameof(IPlayerPresenceGrain.GetActiveRoomAsync)] = call =>
            Task.FromResult(
                new RoomPointerSnapshot
                {
                    RoomId =
                        _asleep.Contains((long)call.Key!) || _inRoom.Contains((long)call.Key!)
                            ? ROOM
                            : -1,
                    ActiveSinceUtc = Login,
                }
            );
        _fakes.Handlers[nameof(IRoomGrain.GetAvatarPositionAsync)] = call =>
            Task.FromResult<RoomAvatarPositionSnapshot?>(
                new RoomAvatarPositionSnapshot
                {
                    RoomId = ROOM,
                    X = 1,
                    Y = 1,
                    Z = default,
                    Rotation = default,
                    IsIdle = _asleep.Contains(((PlayerId)call.Args[0]!).Value),
                    IsWalking = false,
                }
            );

        _service = new OnlineRewardService(
            _db,
            _config,
            new TestOptionsMonitor<OperationsConfig>(new() { MaxCurrencyAmount = 1000 }),
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<ICurrencyTypeProvider>(),
            _time,
            NullLogger<OnlineRewardService>.Instance
        );
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task each_interval_of_time_online_since_login_pays_once()
    {
        Rules(
            new OnlineRewardRule
            {
                Currency = "duckets",
                Amount = 5,
                IntervalMinutes = 1,
            }
        );

        var paid = new List<int>();

        foreach (var seconds in new[] { 10, 30, 59, 60, 70, 119, 125, 180 })
        {
            _time.Set(Login.AddSeconds(seconds));
            paid.Add(await SweepAsync(ALICE));
        }

        paid.Should().Equal(0, 0, 0, 1, 0, 0, 1, 1);
        Credits()
            .Should()
            .AllSatisfy(x =>
            {
                x.Player.Should().Be(ALICE);
                x.Kind.Should().Be(CurrencyKind.ActivityPoints(0));
                x.Amount.Should().Be(5);
                x.Cause.Cause.Should().Be(MovementCauses.REWARD_ONLINE);
            });
    }

    [Fact]
    public async Task nothing_is_paid_while_the_feature_or_the_rule_is_off()
    {
        Rules(
            new OnlineRewardRule
            {
                Currency = "duckets",
                Amount = 5,
                IntervalMinutes = 1,
            }
        );
        _config.Set(
            new OnlineRewardsConfig { Enabled = false, Rules = _config.CurrentValue.Rules }
        );

        await RunMinutesAsync(5, ALICE);

        Rules(
            new OnlineRewardRule
            {
                Currency = "duckets",
                Amount = 5,
                IntervalMinutes = 1,
                Enabled = false,
            }
        );

        await RunMinutesAsync(5, ALICE);

        Credits().Should().BeEmpty();
    }

    [Fact]
    public async Task a_club_member_gets_the_bonus_on_top()
    {
        _club.Add(ALICE);
        Rules(
            new OnlineRewardRule
            {
                Currency = "credits",
                Amount = 5,
                HcBonus = 3,
                IntervalMinutes = 1,
            }
        );

        await RunMinutesAsync(1, ALICE, BOB);

        Credits().Select(x => (x.Player, x.Amount)).Should().BeEquivalentTo([(ALICE, 8), (BOB, 5)]);
    }

    [Fact]
    public async Task the_daily_cap_holds_across_logins_and_starts_again_the_next_day()
    {
        Rules(
            new OnlineRewardRule
            {
                Currency = "duckets",
                Amount = 5,
                IntervalMinutes = 1,
                DailyCap = 12,
            }
        );

        await RunMinutesAsync(4, ALICE);

        Credits().Select(x => x.Amount).Should().Equal(5, 5, 2);

        // Logging in again does not start the day over.
        var relogin = _time.GetUtcNow().UtcDateTime;
        for (var minute = 1; minute <= 3; minute++)
        {
            _time.Set(relogin.AddMinutes(minute));
            await _service.SweepAsync([Online(ALICE, "second", relogin)], Ct);
        }

        Credits().Should().HaveCount(3);

        var nextDay = Login.Date.AddDays(1).AddHours(8);
        _time.Set(nextDay.AddMinutes(1));
        await _service.SweepAsync([Online(ALICE, "third", nextDay)], Ct);

        Credits().Select(x => x.Amount).Should().Equal(5, 5, 2, 5);
        await using var dbCtx = await _db.CreateDbContextAsync(Ct);
        (await dbCtx.PlayerOnlineRewards.SingleAsync(Ct))
            .Should()
            .BeEquivalentTo(
                new { Day = DateOnly.FromDateTime(nextDay), Amount = 5 },
                o => o.ExcludingMissingMembers()
            );
    }

    [Fact]
    public async Task an_idle_or_sleeping_player_misses_the_payout_and_it_is_not_made_up()
    {
        Rules(
            new OnlineRewardRule
            {
                Currency = "duckets",
                Amount = 5,
                IntervalMinutes = 1,
            }
        );
        _config.Set(
            new OnlineRewardsConfig
            {
                Enabled = true,
                Rules = _config.CurrentValue.Rules,
                IdleMinutes = 10,
            }
        );
        _asleep.Add(BOB);

        // Alice last did something at login, so she is idle from minute 10 on; Bob sleeps in his room.
        for (var minute = 1; minute <= 12; minute++)
        {
            _time.Set(Login.AddMinutes(minute));
            await _service.SweepAsync(
                [Online(ALICE, "alice", Login, lastActive: Login), Online(BOB, "bob", Login)],
                Ct
            );
        }

        Credits().Where(x => x.Player == ALICE).Should().HaveCount(9);
        Credits().Where(x => x.Player == BOB).Should().BeEmpty();

        // Bob wakes: only the payout of the next minute, nothing for the minutes he slept.
        _asleep.Remove(BOB);
        _time.Set(Login.AddMinutes(13));
        await SweepAsync(BOB);

        Credits().Where(x => x.Player == BOB).Should().ContainSingle();
    }

    /// <summary>
    /// A hotel that pays for time spent in rooms, not on the hotel view, next to a rule that pays
    /// anywhere: each rule asks for itself.
    /// </summary>
    [Fact]
    public async Task a_rule_for_rooms_only_misses_a_player_on_the_hotel_view()
    {
        Rules(
            new OnlineRewardRule
            {
                Currency = "duckets",
                Amount = 5,
                IntervalMinutes = 1,
                InRoomOnly = true,
            },
            new OnlineRewardRule
            {
                Currency = "credits",
                Amount = 1,
                IntervalMinutes = 1,
            }
        );
        _inRoom.Add(ALICE);

        await RunMinutesAsync(2, ALICE, BOB);

        Credits()
            .Select(x => (x.Player, x.Kind))
            .Should()
            .BeEquivalentTo([
                (ALICE, CurrencyKind.ActivityPoints(0)),
                (ALICE, CurrencyKind.Credits),
                (ALICE, CurrencyKind.ActivityPoints(0)),
                (ALICE, CurrencyKind.Credits),
                (BOB, CurrencyKind.Credits),
                (BOB, CurrencyKind.Credits),
            ]);
    }

    /// <summary>
    /// A rule with its own idle and sleep settings pays by them; the rule without keeps the
    /// hotel's (here: idle after 10 minutes, no pay asleep).
    /// </summary>
    [Fact]
    public async Task a_rules_own_idle_and_sleep_settings_stand_over_the_hotels()
    {
        _config.Set(
            new OnlineRewardsConfig
            {
                Enabled = true,
                IdleMinutes = 10,
                SkipSleeping = true,
                Rules =
                [
                    new OnlineRewardRule
                    {
                        Currency = "duckets",
                        Amount = 5,
                        IntervalMinutes = 1,
                        IdleMinutes = 0,
                        SkipSleeping = false,
                    },
                    new OnlineRewardRule
                    {
                        Currency = "credits",
                        Amount = 1,
                        IntervalMinutes = 1,
                    },
                ],
            }
        );
        _asleep.Add(BOB);

        for (var minute = 1; minute <= 12; minute++)
        {
            _time.Set(Login.AddMinutes(minute));
            await _service.SweepAsync(
                [Online(ALICE, "alice", Login, lastActive: Login), Online(BOB, "bob", Login)],
                Ct
            );
        }

        Credits()
            .GroupBy(x => (x.Player, x.Kind))
            .ToDictionary(x => x.Key, x => x.Count())
            .Should()
            .BeEquivalentTo(
                new Dictionary<(int, CurrencyKind), int>
                {
                    [(ALICE, CurrencyKind.ActivityPoints(0))] = 12,
                    [(ALICE, CurrencyKind.Credits)] = 9,
                    [(BOB, CurrencyKind.ActivityPoints(0))] = 12,
                }
            );
    }

    [Fact]
    public async Task a_server_that_falls_behind_pays_one_interval_not_the_backlog()
    {
        Rules(
            new OnlineRewardRule
            {
                Currency = "duckets",
                Amount = 5,
                IntervalMinutes = 1,
            }
        );

        _time.Set(Login.AddSeconds(10));
        await SweepAsync(ALICE);
        _time.Set(Login.AddMinutes(10));
        await SweepAsync(ALICE);

        Credits().Should().ContainSingle();
    }

    [Fact]
    public async Task the_player_is_sent_the_activity_point_notification_with_the_change()
    {
        var wallet = await RealWalletAsync(ALICE);
        _fakes.Instances[(typeof(IPlayerWalletGrain), (long)ALICE)] = wallet;
        Rules(
            new OnlineRewardRule
            {
                Currency = "duckets",
                Amount = 5,
                IntervalMinutes = 1,
            }
        );

        await RunMinutesAsync(2, ALICE);

        _fakes
            .SentToPlayer<HabboActivityPointNotificationMessageComposer>(ALICE)
            .Select(x => (x.ActivityPointType, x.Amount, x.Change))
            .Should()
            .Equal((0, 5, 5), (0, 10, 5));
        await using var dbCtx = await _db.CreateDbContextAsync(Ct);
        (
            await dbCtx
                .AssetMovementOutbox.Where(x => x.Cause == MovementCauses.REWARD_ONLINE)
                .CountAsync(Ct)
        )
            .Should()
            .Be(2);
    }

    private void Rules(params OnlineRewardRule[] rules) =>
        _config.Set(new OnlineRewardsConfig { Enabled = true, Rules = [.. rules] });

    private async Task RunMinutesAsync(int minutes, params int[] players)
    {
        for (var minute = 1; minute <= minutes; minute++)
        {
            _time.Set(Login.AddMinutes(minute));
            await SweepAsync(players);
        }
    }

    private Task<int> SweepAsync(params int[] players) =>
        _service.SweepAsync([.. players.Select(x => Online(x, $"s{x}", Login))], Ct);

    private OnlinePlayer Online(
        int playerId,
        string session,
        DateTime since,
        DateTime? lastActive = null
    ) => new(playerId, new SessionKey(session), since, lastActive ?? _time.GetUtcNow().UtcDateTime);

    private List<(int Player, CurrencyKind Kind, int Amount, MovementCause Cause)> Credits() =>
        [
            .. _fakes
                .Log.Of(nameof(IPlayerWalletGrain.CreditAsync))
                .Where(x => x.Args.Length == 4)
                .Select(x =>
                    (
                        (int)(long)x.Key!,
                        (CurrencyKind)x.Args[0]!,
                        (int)x.Args[1]!,
                        (MovementCause)x.Args[2]!
                    )
                ),
        ];

    private async Task<IPlayerWalletGrain> RealWalletAsync(int playerId)
    {
        var recorder = new AssetMovementRecorder(
            new TestOptionsMonitor<MovementsConfig>(new() { Enabled = true }),
            _db,
            NullLogger<AssetMovementRecorder>.Instance
        );
        await recorder.StartAsync(Ct);

        var grain = GrainHarness.Create<PlayerWalletGrain>(_fakes, _db, playerId);
        GrainHarness.Replace<ICurrencyTypeProvider>(grain, _fakes.Create<ICurrencyTypeProvider>());
        GrainHarness.Replace<IPlayerNoticeService>(grain, _fakes.Create<IPlayerNoticeService>());
        GrainHarness.Replace<IAssetMovementRecorder>(grain, recorder);
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerWalletGrain)grain;
    }
}
