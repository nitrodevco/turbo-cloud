using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Guilds.Grains;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Users;
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
using Turbo.Primitives.Players.Grains.Guilds;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// Joining an open group answers the joiner the way Habbo does: the official account joined open
/// group 368818 twice, standing in its homeroom, and got WiredPermissions,
/// RaidProtectionCapability, GuildMemberships, HabboGroupDetails, HabboGroupBadges,
/// FavoriteMembershipUpdate and ExtendedProfileChanged, with no GuildMembershipUpdated and no
/// GroupDetailsChanged (official client capture 2026-10-13 20:55 and 21:07). The rights come from
/// the homeroom; this covers what the join itself sends. Turbo sent the joiner
/// GuildMembershipUpdated and GroupDetailsChanged, never their group list, and never said their
/// profile changed.
///
/// A joiner wearing no group wears the new one (GuildMemberships listed 402778 as favourite after
/// the join, official client capture 2026-10-13 14:21), but only after the details: Habbo's
/// details still said not favourite and the badge messages followed them. Turbo wore it inside
/// the join, so the room heard the badge before the joiner had the details. A player who already
/// wears a group keeps it.
/// </summary>
public sealed class GroupJoinRepliesTests : IDisposable
{
    private const int GROUP = 10;
    private const int OWNER = 1;
    private const int ALICE = 2;
    private const int BOB = 3;
    private const int CAROL = 4;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GroupJoinRepliesTests()
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
        // The group's homeroom, and one for the other group a player may already wear.
        foreach (var (roomId, roomName) in new[] { (9, "homeroom"), (10, "other homeroom") })
            _db.Insert(
                new RoomEntity
                {
                    Id = roomId,
                    Name = roomName,
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
    public async Task Joining_answers_with_the_groups_the_details_and_profile_changed()
    {
        await GroupAsync();

        await JoinAsync(CAROL);

        _fakes
            .Sent(SendRoute.Session)
            .Select(x => x.GetType().Name)
            .Should()
            .Equal(
                nameof(GuildMembershipsMessageComposer),
                nameof(HabboGroupDetailsMessageComposer)
            );
        var details = _fakes.Sent<HabboGroupDetailsMessageComposer>(SendRoute.Session).Single();
        details.View.Status.Should().Be(GuildMembershipStatus.Member);
        details.OpenDetails.Should().BeFalse();

        // Through the presence, behind the room's badge messages (GroupReplyOrderTests).
        _fakes
            .SentToPlayer<IComposer>(CAROL)
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<ExtendedProfileChangedMessageComposer>()
            .Which.UserId.Should()
            .Be(CAROL);
    }

    [Fact]
    public async Task The_badge_is_worn_after_the_details_and_before_the_profile_changed()
    {
        await GroupAsync();

        await JoinAsync(CAROL);

        var calls = _fakes.Log.Calls.ToList();
        var details = calls.FindIndex(x =>
            x.Args.FirstOrDefault() is HabboGroupDetailsMessageComposer
        );
        var wear = calls.FindIndex(x =>
            x.Method == "WearIfNoneAsync" && Equals(x.Args[0], new GuildId(GROUP))
        );
        var profile = calls.FindIndex(x =>
            x.Args.FirstOrDefault() is ExtendedProfileChangedMessageComposer
        );

        wear.Should().BeGreaterThan(details).And.BeLessThan(profile);
        // Habbo's details still said not favourite: the badge is put on after them.
        _fakes
            .Sent<HabboGroupDetailsMessageComposer>(SendRoute.Session)
            .Single()
            .IsFavourite.Should()
            .BeFalse();
    }

    [Fact]
    public async Task A_first_group_joined_is_worn()
    {
        await GroupAsync();
        await CarolsGroupsAsync();

        await JoinAsync(CAROL);

        (await WearsAsync(CAROL, GROUP)).Should().BeTrue();
    }

    [Fact]
    public async Task A_player_wearing_another_group_keeps_it()
    {
        _db.Insert(
            new GuildEntity
            {
                Id = 77,
                Name = "Other",
                Description = string.Empty,
                BadgeCode = "b05114s19134",
                PrimaryColorId = 1,
                SecondaryColorId = 1,
                GuildType = GuildType.Regular,
                RightsLevel = GuildRightsLevel.Owner,
                PlayerEntityId = OWNER,
                RoomEntityId = 10,
            }
        );
        _db.Insert(
            new GuildMemberEntity
            {
                Id = 9,
                GuildEntityId = 77,
                PlayerEntityId = CAROL,
                Rank = GuildMemberRank.Member,
                IsFavourite = true,
            }
        );
        await GroupAsync();
        await CarolsGroupsAsync();

        await JoinAsync(CAROL);

        (await WearsAsync(CAROL, GROUP)).Should().BeFalse();
        (await WearsAsync(CAROL, 77)).Should().BeTrue();
    }

    [Fact]
    public async Task The_joiner_hears_no_membership_updated_but_the_managers_do()
    {
        await GroupAsync();

        await JoinAsync(CAROL);

        _fakes.SentToPlayer<GuildMembershipUpdatedMessageComposer>(CAROL).Should().BeEmpty();

        foreach (var manager in new[] { OWNER, BOB })
            _fakes
                .SentToPlayer<GuildMembershipUpdatedMessageComposer>(manager)
                .Should()
                .ContainSingle(x => x.Member.PlayerId == PlayerId.Parse(CAROL));
    }

    [Fact]
    public async Task A_request_to_an_exclusive_group_still_refreshes_the_details()
    {
        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
        {
            dbCtx.Guilds.Single(x => x.Id == GROUP).GuildType = GuildType.Exclusive;
            await dbCtx.SaveChangesAsync(Ct);
        }

        await GroupAsync();

        await JoinAsync(CAROL);

        _fakes
            .Sent(SendRoute.Session)
            .Select(x => x.GetType().Name)
            .Should()
            .Equal(nameof(GroupDetailsChangedMessageComposer));
    }

    private async Task JoinAsync(int player) =>
        await new JoinHabboGroupMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            new() { GuildId = new GuildId(GROUP) },
            new MessageContext(_fakes.Create<ISessionContext>(), player, -1),
            Ct
        );

    private async Task<bool> WearsAsync(int player, int group)
    {
        await using var dbCtx = await _db.CreateDbContextAsync(Ct);

        return dbCtx
            .GuildMembers.Single(x => x.GuildEntityId == group && x.PlayerEntityId == player)
            .IsFavourite;
    }

    /// <summary>Carol's own group grain, live, so the join and the badge reach the database.</summary>
    private async Task CarolsGroupsAsync()
    {
        _fakes.Handlers["GetSummariesAsync"] = _ =>
            Task.FromResult(ImmutableArray<GuildSummarySnapshot>.Empty);
        // Standing in the group's homeroom, as the official account was.
        _fakes.Handlers["GetActiveRoomAsync"] = _ =>
            Task.FromResult(
                new RoomPointerSnapshot { RoomId = 9, ActiveSinceUtc = DateTime.UtcNow }
            );

        var grain = GrainHarness.Create<PlayerGuildGrain>(_fakes, _db, CAROL);
        await ((Grain)grain).OnActivateAsync(Ct);

        _fakes.Instances[(typeof(IPlayerGuildGrain), (long)CAROL)] = grain;
    }

    private async Task GroupAsync()
    {
        _fakes.Handlers["GetSummaryAsync"] = call =>
            call.Interface == typeof(IRoomGrain)
                ? Task.FromResult(
                    new RoomSummarySnapshot
                    {
                        RoomId = 9,
                        Name = "homeroom",
                        Description = string.Empty,
                        OwnerId = OWNER,
                        OwnerName = "owner",
                        Population = 0,
                        LastUpdatedUtc = DateTime.UtcNow,
                    }
                )
                : Fakes.NotHandled;
        _fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult("owner");
        _fakes.Handlers["GetMembershipsAsync"] = _ =>
            Task.FromResult(ImmutableArray<GuildInfoSnapshot>.Empty);

        var grain = GrainHarness.Create<GuildGrain>(_fakes, _db);
        GrainHarness.Replace(grain, new GuildLiveState { GuildId = new GuildId(GROUP) });

        await ((Grain)grain).OnActivateAsync(Ct);

        _fakes.Instances[(typeof(IGuildGrain), (long)GROUP)] = grain;
    }
}
