using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Guilds;
using Turbo.Guilds.Configuration;
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

namespace Turbo.Tests.Guilds;

/// <summary>
/// Joining a group while wearing no group badge wears the new one: the official account, in no
/// group, joined open group 402778 through the client and GuildMemberships then listed it with
/// favourite:true (official client capture 2026-10-13, 14:21). Turbo stored every join as not
/// favourite, so a first group showed no badge until it was picked by hand. A player who already
/// wears a group keeps it.
/// </summary>
public sealed class GroupJoinFavouriteTests : IDisposable
{
    private const int GROUP = 10;
    private const int OWNER = 1;
    private const int ALICE = 2;
    private const int BOB = 3;
    private const int CAROL = 4;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GroupJoinFavouriteTests()
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

    private async Task<bool> CarolWearsTheGroupAsync()
    {
        await using var dbCtx = await _db.CreateDbContextAsync(Ct);

        return dbCtx
            .GuildMembers.Single(x => x.GuildEntityId == GROUP && x.PlayerEntityId == CAROL)
            .IsFavourite;
    }

    [Fact]
    public async Task A_first_group_joined_is_worn()
    {
        var group = await GroupAsync();

        await group.JoinAsync(CAROL, Ct);

        (await CarolWearsTheGroupAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task A_player_wearing_another_group_keeps_it()
    {
        _fakes.Handlers["GetFavouriteGuildIdAsync"] = _ =>
            Task.FromResult<GuildId?>(new GuildId(77));
        var group = await GroupAsync();

        await group.JoinAsync(CAROL, Ct);

        (await CarolWearsTheGroupAsync()).Should().BeFalse();
    }

    private async Task<IGuildGrain> GroupAsync(GuildConfig? config = null)
    {
        var grain = GrainHarness.Create(
            typeof(GuildModule).Assembly,
            "Turbo.Guilds.Grains.GuildGrain",
            _fakes,
            _db
        );
        var state = grain
            .GetType()
            .GetField(
                "_state",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .GetValue(grain)!;

        RoomHarness.SetMember(state, "GuildId", new GuildId(GROUP));

        if (config is not null)
            RoomHarness.SetField(grain, "_guildConfig", config);

        await ((Grain)grain).OnActivateAsync(Ct);

        return (IGuildGrain)grain;
    }
}
