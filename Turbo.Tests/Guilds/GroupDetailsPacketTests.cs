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
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// The group details packet ends the way Habbo's does: after the 19 fields the client reads
/// (HabboGroupDetailsData.as:63-82) Habbo sends one more int, 50000 for every group (official
/// client capture 2026-10-13: 13 samples, 9 groups, member and not). Turbo stopped at the forum
/// flag, four bytes short.
/// </summary>
public sealed class GroupDetailsPacketTests : IDisposable
{
    private const int GROUP = 10;
    private const int OWNER = 1;
    private const int ALICE = 2;
    private const int BOB = 3;
    private const int CAROL = 4;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GroupDetailsPacketTests()
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
    public async Task Group_details_end_with_the_member_limit()
    {
        var packet = await DetailsAsync(await GroupAsync());

        SkipTheFieldsTheClientReads(packet);

        packet.PopInt().Should().Be(50000);
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task The_member_limit_is_the_hotels_to_set()
    {
        var packet = await DetailsAsync(
            await GroupAsync(new GuildConfig { DetailsMemberLimit = 1234 })
        );

        SkipTheFieldsTheClientReads(packet);

        packet.PopInt().Should().Be(1234);
    }

    /// <summary>The 19 fields of HabboGroupDetailsData, in its order.</summary>
    private static void SkipTheFieldsTheClientReads(ClientPacket packet)
    {
        packet.PopInt().Should().Be(GROUP);
        packet.PopBoolean();
        packet.PopInt();
        packet.PopString().Should().Be("Rude name");
        packet.PopString();
        packet.PopString();
        packet.PopInt();
        packet.PopString();
        packet.PopInt();
        packet.PopInt();
        packet.PopBoolean();
        packet.PopString();
        packet.PopBoolean();
        packet.PopBoolean();
        packet.PopString().Should().Be("owner");
        packet.PopBoolean();
        packet.PopBoolean();
        packet.PopInt();
        packet.PopBoolean();
    }

    private static async Task<ClientPacket> DetailsAsync(IGuildGrain group)
    {
        var view = await group.GetViewAsync(ALICE, Ct);

        return PacketHarness.Encode(
            new HabboGroupDetailsMessageComposer
            {
                View = view!,
                RoomName = "homeroom",
                OwnerName = "owner",
                IsFavourite = false,
                OpenDetails = true,
            }
        );
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
