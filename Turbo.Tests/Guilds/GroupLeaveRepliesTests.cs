using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Guilds.Configuration;
using Turbo.Guilds.Grains;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// Leaving a group answers the way Habbo does: the official account left open group 402778 and
/// got GuildMembershipRejected(group, self), ExtendedProfileChanged(self) and
/// GroupDetailsChanged(group) in one tick, with no GuildMembershipUpdated (official client capture
/// 2026-10-13, 14:23). Turbo sent a GuildMembershipUpdated drawing the leaver as a pending request
/// and never said their profile changed. The managers' members page refreshes on the same
/// GuildMembershipRejected; that part is not recorded. A member kicked, made an admin or unmade
/// one by somebody else hears nothing from the group, only their homeroom rights (official client
/// capture 2026-10-13 20:56 and 21:10); Turbo sent them the leaver's replies and
/// GuildMembershipUpdated. Blocking is off unless configured, as on Habbo.
/// </summary>
public sealed class GroupLeaveRepliesTests : IDisposable
{
    private const int GROUP = 10;
    private const int OWNER = 1;
    private const int ALICE = 2;
    private const int BOB = 3;
    private const int CAROL = 4;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GroupLeaveRepliesTests()
    {
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

        foreach (
            var (id, name) in new[]
            {
                (OWNER, "owner"),
                (ALICE, "alice"),
                (BOB, "bob"),
                (CAROL, "carol"),
            }
        )
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
                Name = "Rude name",
                Description = "Rude words",
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
                (3, BOB, GuildMemberRank.Admin),
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
    public async Task Leaving_tells_the_leaver_rejected_profile_changed_and_details_changed()
    {
        var group = await GroupAsync();

        var result = await group.KickAsync(ALICE, ALICE, false, Ct);

        result.Should().Be(GuildMemberMgmtResultSnapshot.Success());
        Sent(ALICE)
            .Select(x => x.GetType().Name)
            .Should()
            .Equal(
                nameof(GuildMembershipRejectedMessageComposer),
                nameof(ExtendedProfileChangedMessageComposer),
                nameof(GroupDetailsChangedMessageComposer)
            );
        var rejected = Sent(ALICE).OfType<GuildMembershipRejectedMessageComposer>().Single();
        rejected.GuildId.Should().Be(new GuildId(GROUP));
        rejected.PlayerId.Should().Be(PlayerId.Parse(ALICE));
        Sent(ALICE)
            .OfType<ExtendedProfileChangedMessageComposer>()
            .Single()
            .UserId.Should()
            .Be(ALICE);
    }

    [Fact]
    public async Task The_managers_are_told_the_member_is_gone_not_pending()
    {
        var group = await GroupAsync();

        await group.KickAsync(ALICE, ALICE, false, Ct);

        foreach (var manager in new[] { OWNER, BOB })
        {
            Sent(manager).OfType<GuildMembershipUpdatedMessageComposer>().Should().BeEmpty();
            Sent(manager)
                .OfType<GuildMembershipRejectedMessageComposer>()
                .Should()
                .ContainSingle(x => x.PlayerId == PlayerId.Parse(ALICE));
        }
    }

    [Fact]
    public async Task A_blocked_member_is_still_shown_as_blocked()
    {
        var group = await GroupAsync(new GuildConfig { BlockingEnabled = true });

        await group.KickAsync(OWNER, ALICE, true, Ct);

        Sent(OWNER)
            .OfType<GuildMembershipUpdatedMessageComposer>()
            .Should()
            .ContainSingle(x => x.Member.Rank == GuildMemberRank.Blocked);
    }

    [Fact]
    public async Task A_member_kicked_by_an_admin_hears_nothing_from_the_group()
    {
        var group = await GroupAsync();

        var result = await group.KickAsync(BOB, ALICE, false, Ct);

        result.Should().Be(GuildMemberMgmtResultSnapshot.Success());
        Sent(ALICE).Should().BeEmpty();
        Sent(OWNER)
            .OfType<GuildMembershipRejectedMessageComposer>()
            .Should()
            .ContainSingle(x => x.PlayerId == PlayerId.Parse(ALICE));
        HomeroomRefreshed().Should().Contain(PlayerId.Parse(ALICE));
    }

    [Theory]
    [InlineData(ALICE, true)]
    [InlineData(BOB, false)]
    public async Task A_member_made_or_unmade_an_admin_hears_nothing_from_the_group(
        int target,
        bool isAdmin
    )
    {
        var group = await GroupAsync();

        var result = await group.SetAdminAsync(OWNER, target, isAdmin, Ct);

        result.Should().Be(GuildMemberMgmtResultSnapshot.Success());
        Sent(target).Should().BeEmpty();
        Sent(OWNER)
            .OfType<GuildMembershipUpdatedMessageComposer>()
            .Should()
            .ContainSingle(x =>
                x.Member.Rank == (isAdmin ? GuildMemberRank.Admin : GuildMemberRank.Member)
            );
        HomeroomRefreshed().Should().Contain(PlayerId.Parse(target));
    }

    [Fact]
    public async Task Blocking_is_off_unless_configured_so_a_block_only_removes()
    {
        var group = await GroupAsync();

        await group.KickAsync(OWNER, ALICE, true, Ct);

        Sent(OWNER).OfType<GuildMembershipUpdatedMessageComposer>().Should().BeEmpty();
        Sent(OWNER)
            .OfType<GuildMembershipRejectedMessageComposer>()
            .Should()
            .ContainSingle(x => x.PlayerId == PlayerId.Parse(ALICE));
    }

    private List<PlayerId> HomeroomRefreshed() =>
        [.. _fakes.Log.Of("RefreshGuildMemberAsync").Select(x => (PlayerId)x.Args[0]!)];

    private List<IComposer> Sent(int player) =>
        [
            .. _fakes
                .Log.Of("SendComposerAsync")
                .Where(x => Convert.ToInt64(x.Key) == player)
                .Select(x => x.Args[0])
                .OfType<IComposer>(),
        ];

    private async Task<IGuildGrain> GroupAsync(GuildConfig? config = null)
    {
        var grain = GrainHarness.Create<GuildGrain>(_fakes, _db);
        GrainHarness.Replace(grain, new GuildLiveState { GuildId = new GuildId(GROUP) });

        if (config is not null)
            GrainHarness.Replace<GuildConfig>(grain, config);

        await ((Grain)grain).OnActivateAsync(Ct);

        return (IGuildGrain)grain;
    }
}
