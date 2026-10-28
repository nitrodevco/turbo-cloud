using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Staff fixing a group from the panel, through the group's own grain: renamed, its badge put
/// back, a member taken out, or the group deleted whatever its size or the hotel's deletion
/// setting - each told to the directory, the members and the owner as the owner's own change
/// would be. And the parts and colours group badges are built from, added under the next id and
/// read again by the directory at once.
/// </summary>
public sealed class GroupStaffTests : IDisposable
{
    private const int GROUP = 10;
    private const int OWNER = 1;
    private const int ALICE = 2;
    private const int BOB = 3;

    private static readonly PlayerId STAFF = new(99);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GroupStaffTests()
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

        foreach (var (id, name) in new[] { (OWNER, "owner"), (ALICE, "alice"), (BOB, "bob") })
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
    public async Task A_rename_is_saved_and_told_as_the_owners_own_change_would_be()
    {
        var group = await GroupAsync();

        (await group.StaffRenameAsync(STAFF, "  Clean name  ", "A fine group", Ct))
            .Should()
            .BeTrue();

        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
            (await dbCtx.Guilds.SingleAsync(Ct)).Name.Should().Be("Clean name");

        (await group.GetSnapshotAsync(Ct))!.Name.Should().Be("Clean name");
        _fakes
            .Log.Of("OnGuildChangedAsync")
            .Should()
            .NotBeEmpty("the directory learns the new name");
        _fakes.SentToPlayer<GroupDetailsChangedMessageComposer>(OWNER).Should().NotBeEmpty();

        (await group.StaffRenameAsync(STAFF, "   ", "x", Ct))
            .Should()
            .BeFalse("a group needs a name");
    }

    [Fact]
    public async Task A_member_is_taken_out_but_the_owner_is_not()
    {
        var group = await GroupAsync();

        (await group.StaffRemoveMemberAsync(STAFF, new PlayerId(OWNER), Ct)).Should().BeFalse();
        (await group.StaffRemoveMemberAsync(STAFF, new PlayerId(BOB), Ct))
            .Should()
            .BeTrue("staff remove admins too");

        await using var dbCtx = await _db.CreateDbContextAsync(Ct);

        dbCtx.GuildMembers.Select(x => x.PlayerEntityId).Should().BeEquivalentTo([OWNER, ALICE]);
        _fakes
            .Log.Of("OnMembershipsChangedAsync")
            .Should()
            .Contain(x => (long)x.Key! == BOB, "the player's own grain forgets the membership");
    }

    [Fact]
    public async Task Staff_delete_a_group_the_hotel_would_not_let_its_owner_delete()
    {
        var group = await GroupAsync(
            new GuildConfig { DeletionEnabled = false, DeletionMaxMembers = 1 }
        );

        (await group.DeactivateAsync(new PlayerId(OWNER), Ct)).Should().BeFalse();
        (await group.StaffDeleteAsync(STAFF, Ct)).Should().BeTrue();

        await using var dbCtx = await _db.CreateDbContextAsync(Ct);

        dbCtx.Guilds.Should().BeEmpty();
        dbCtx.GuildMembers.Should().BeEmpty();
        _fakes.Log.Of("OnGuildRemovedAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task Deleting_a_group_takes_its_forum_and_closes_the_forum_grain()
    {
        _db.Insert(new GuildForumEntity { GuildEntityId = GROUP });
        _db.Insert(
            new GuildForumThreadEntity
            {
                Id = 1,
                GuildEntityId = GROUP,
                PlayerEntityId = ALICE,
                Subject = "A first subject",
            }
        );
        _db.Insert(
            new GuildForumMessageEntity
            {
                GuildEntityId = GROUP,
                ThreadEntityId = 1,
                ForumMessageId = 1,
                ThreadIndex = 0,
                PlayerEntityId = ALICE,
                Text = "A message long enough",
            }
        );
        _db.Insert(new GuildForumReadMarkerEntity { PlayerEntityId = BOB, GuildEntityId = GROUP });

        var group = await GroupAsync();

        (await group.StaffDeleteAsync(STAFF, Ct)).Should().BeTrue();

        await using var dbCtx = await _db.CreateDbContextAsync(Ct);

        dbCtx.GuildForums.Should().BeEmpty();
        dbCtx.GuildForumThreads.Should().BeEmpty();
        dbCtx.GuildForumMessages.Should().BeEmpty();
        dbCtx.GuildForumReadMarkers.Should().BeEmpty();
        _fakes
            .Log.Of(nameof(IGuildForumGrain.OnGuildDeletedAsync))
            .Where(x => x.Interface == typeof(IGuildForumGrain))
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task A_badge_is_put_back_to_the_editors_default()
    {
        ImmutableArray<GuildBadgePartSnapshot> defaults =
        [
            new GuildBadgePartSnapshot
            {
                Type = GuildBadgePartType.Base,
                PartId = 1,
                ColorId = 1,
                Position = 0,
            },
        ];
        _fakes.Handlers["GetDefaultBadgePartsAsync"] = _ => Task.FromResult(defaults);

        var group = await GroupAsync();

        (await group.StaffResetBadgeAsync(STAFF, Ct)).Should().BeTrue();

        (await group.GetSnapshotAsync(Ct))!.BadgeCode.Should().Be(GuildBadgeCodes.Build(defaults));
    }

    [Fact]
    public async Task A_part_or_colour_is_added_under_the_next_id_and_the_directory_reads_them_again()
    {
        _db.Insert(
            new GuildBadgePartEntity
            {
                Id = 1,
                PartType = GuildBadgePartType.Symbol,
                PartId = 159,
                FileName = "symbol_x",
                MaskFileName = "",
            }
        );

        var queries = new AdminGroupQueries(
            _db,
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IGamedataFileService>()
        );

        await queries.SavePartAsync(
            0,
            new GroupBadgePartRequest(GuildBadgePartType.Symbol, "symbol_new", null),
            Ct
        );
        await queries.SaveColorAsync(
            0,
            new GroupColorRequest(GuildColorSlotType.Primary, "#FF8800"),
            Ct
        );

        var editor = await queries.GetEditorAsync(Ct);

        editor.Parts.Should().Contain(x => x.FileName == "symbol_new" && x.PartId == 160);
        editor
            .Colors.Should()
            .ContainSingle()
            .Which.Should()
            .Be(editor.Colors[0] with { ColorId = 1, Color = "ff8800" });
        _fakes.Log.Of("ReloadAsync").Should().HaveCount(2);
        _fakes
            .Log.Of(nameof(IGamedataFileService.Invalidate))
            .Select(x => x.Args[0])
            .Should()
            .Equal(GamedataFiles.BADGE_PARTS, GamedataFiles.BADGE_PARTS);

        var bad = () =>
            queries.SaveColorAsync(
                0,
                new GroupColorRequest(GuildColorSlotType.Primary, "orange"),
                Ct
            );

        await bad.Should().ThrowAsync<ArgumentException>();
        (await queries.SearchAsync("Rude", 0, null, null, Ct))
            .Groups.Should()
            .ContainSingle()
            .Which.Members.Should()
            .Be(3);
    }

    [Fact]
    public async Task A_found_group_names_its_home_room_and_kind_for_the_list()
    {
        var queries = new AdminGroupQueries(
            _db,
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IGamedataFileService>()
        );

        var found = (await queries.SearchAsync("9", 0, null, null, Ct))
            .Groups.Should()
            .ContainSingle()
            .Which;

        found.RoomName.Should().Be("homeroom");
        found.Type.Should().Be(GuildType.Regular);
    }

    [Fact]
    public async Task Groups_SortByMembers_CountedAsTheListShowsThem()
    {
        // A newer group in a room of its own, with one member and a pending request.
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            var room = await db.Rooms.AsNoTracking().SingleAsync(x => x.Id == 9, Ct);

            room.Id = 8;
            room.Name = "otherroom";
            _db.Insert(room);
        }

        _db.Insert(
            new GuildEntity
            {
                Id = GROUP + 1,
                Name = "Quiet",
                Description = "",
                BadgeCode = "b05114s19134",
                PrimaryColorId = 1,
                SecondaryColorId = 1,
                GuildType = GuildType.Regular,
                RightsLevel = GuildRightsLevel.Owner,
                PlayerEntityId = ALICE,
                RoomEntityId = 8,
            }
        );

        foreach (
            var (id, player, rank) in new[]
            {
                (4, ALICE, GuildMemberRank.Owner),
                (5, BOB, GuildMemberRank.Requested),
            }
        )
            _db.Insert(
                new GuildMemberEntity
                {
                    Id = id,
                    GuildEntityId = GROUP + 1,
                    PlayerEntityId = player,
                    Rank = rank,
                    IsFavourite = false,
                }
            );

        var queries = new AdminGroupQueries(
            _db,
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IGamedataFileService>()
        );

        var newest = await queries.SearchAsync(null, 0, null, null, Ct);

        newest.Groups.Select(x => x.Id).Should().Equal(GROUP + 1, GROUP);
        newest.Order.Should().Be(new ListOrder("created", ListOrder.DESC));
        (await queries.SearchAsync(null, 0, "members", null, Ct))
            .Groups.Select(x => (x.Id, x.Members))
            .Should()
            .Equal((GROUP, 3), (GROUP + 1, 1));
        (await queries.SearchAsync(null, 0, "members", ListOrder.ASC, Ct))
            .Groups.Select(x => x.Id)
            .Should()
            .Equal(GROUP + 1, GROUP);
    }

    /// <summary>The group's grain, built without a silo and activated, so it has read the group and its roster.</summary>
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
