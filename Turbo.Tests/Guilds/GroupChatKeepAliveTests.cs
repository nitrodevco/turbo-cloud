using FluentAssertions;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Guilds.Grains;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// Who listens to a group's chat lives only in the group grain. Its listener check was a
/// <c>KeepAlive</c> timer every 5 min, and a <c>KeepAlive</c> tick only resets the idle clock, so
/// the group was collected after 2 idle minutes and its chat went nowhere until each messenger
/// joined again. Each join, and each check that leaves someone listening, now pins it
/// (<c>DelayDeactivation</c>, which holds whatever the idle clock says) for two check periods,
/// and the last listener leaving hands it back to idle collection.
/// </summary>
public sealed class GroupChatKeepAliveTests : IDisposable
{
    private const int GROUP = 10;
    private const int OWNER = 1;
    private const int ALICE = 2;

    /// <summary>Two of GuildConfig.ChatListenerCheckMs as shipped (5 min).</summary>
    private static readonly TimeSpan SHIPPED_PIN = TimeSpan.FromMinutes(10);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly HashSet<long> _offline = [];
    private bool _presenceFails;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GroupChatKeepAliveTests()
    {
        _fakes.Handlers["HasActiveSessionAsync"] = call =>
            _presenceFails
                ? Task.FromException<bool>(new TimeoutException())
                : Task.FromResult(!_offline.Contains(Convert.ToInt64(call.Key)));
        _fakes.Handlers["GetEditorDataAsync"] = _ =>
            Task.FromResult(
                new GuildEditorDataSnapshot
                {
                    BaseParts = [],
                    SymbolParts = [],
                    BadgeColors = [],
                    PrimaryColors = [],
                    SecondaryColors = [],
                }
            );

        foreach (var (id, name) in new[] { (OWNER, "owner"), (ALICE, "alice") })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = 9,
                Name = "homeroom",
                PlayerEntityId = OWNER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
        _db.Insert(
            new GuildEntity
            {
                Id = GROUP,
                Name = "Chatty",
                Description = "",
                BadgeCode = "b05114s19134",
                PrimaryColorId = 1,
                SecondaryColorId = 1,
                GuildType = GuildType.Regular,
                RightsLevel = GuildRightsLevel.Owner,
                PlayerEntityId = OWNER,
                RoomEntityId = 9,
            }
        );

        foreach (
            var (id, player, rank) in new[]
            {
                (1, OWNER, GuildMemberRank.Owner),
                (2, ALICE, GuildMemberRank.Member),
            }
        )
            _db.Insert(
                new GuildMemberEntity
                {
                    Id = id,
                    GuildEntityId = GROUP,
                    PlayerEntityId = player,
                    Rank = rank,
                    IsFavourite = false,
                }
            );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task JoiningTheChat_PinsTheGroup()
    {
        var group = await GroupAsync();

        (await group.JoinChatAsync(ALICE, Ct)).Should().BeTrue();

        Pins().Should().Equal([SHIPPED_PIN], "an idle collection would forget who listens");
    }

    [Fact]
    public async Task ACheckThatLeavesSomeoneListening_RenewsThePin()
    {
        var group = await GroupAsync();
        await group.JoinChatAsync(OWNER, Ct);
        await group.JoinChatAsync(ALICE, Ct);
        _offline.Add(ALICE);

        await ListenerCheck().TickAsync(Ct);

        Pins().Should().Equal(SHIPPED_PIN, SHIPPED_PIN, SHIPPED_PIN);
    }

    [Fact]
    public async Task ACheckThatCannotAskThePresence_KeepsThePin()
    {
        var group = await GroupAsync();
        await group.JoinChatAsync(ALICE, Ct);
        _presenceFails = true;

        await ListenerCheck().TickAsync(Ct);

        Pins().Should().Equal(SHIPPED_PIN, SHIPPED_PIN);
    }

    [Fact]
    public async Task TheLastListenerLeaving_HandsTheGroupBackToIdleCollection()
    {
        var group = await GroupAsync();
        await group.JoinChatAsync(OWNER, Ct);
        await group.JoinChatAsync(ALICE, Ct);

        await group.LeaveChatAsync(ALICE, Ct);
        Pins().Should().Equal(SHIPPED_PIN, SHIPPED_PIN);

        await group.LeaveChatAsync(OWNER, Ct);
        Pins().Should().Equal(SHIPPED_PIN, SHIPPED_PIN, TimeSpan.Zero);
    }

    [Fact]
    public async Task ACheckThatFindsNobodyOnline_HandsTheGroupBackToIdleCollection()
    {
        var group = await GroupAsync();
        await group.JoinChatAsync(ALICE, Ct);
        _offline.Add(ALICE);

        await ListenerCheck().TickAsync(Ct);

        Pins().Should().Equal(SHIPPED_PIN, TimeSpan.Zero);
    }

    private sealed record Check(Func<object?, CancellationToken, Task> Callback, object? State)
    {
        public Task TickAsync(CancellationToken ct) => Callback(State, ct);
    }

    private Check ListenerCheck() =>
        _fakes
            .Log.Of("RegisterGrainTimer")
            .Where(call =>
                call.Args[3] is GrainTimerCreationOptions { Period: var period }
                && period != Timeout.InfiniteTimeSpan
            )
            .Select(call => new Check(
                (Func<object?, CancellationToken, Task>)call.Args[1]!,
                call.Args[2]
            ))
            .Single();

    private List<TimeSpan> Pins() =>
        [.. _fakes.Log.Of("DelayDeactivation").Select(call => (TimeSpan)call.Args[1]!)];

    private async Task<IGuildGrain> GroupAsync()
    {
        var grain = GrainHarness.Create<GuildGrain>(_fakes, _db);
        GrainHarness.Replace(grain, new GuildLiveState { GuildId = new GuildId(GROUP) });

        await ((Grain)grain).OnActivateAsync(Ct);

        return (IGuildGrain)grain;
    }
}
