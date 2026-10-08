using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Inventory.Furni;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A wallpaper, floor or landscape used from the inventory (<c>RequestRoomPropertySet</c>):
/// the room's owner may apply it, everyone in the room sees the new pattern, it is saved on the
/// room, and the item is used up. Anyone else, or any other furni, changes nothing.
/// </summary>
public sealed class RoomDecorationTests : IDisposable
{
    private const int OWNER = 1;
    private const int GUEST = 2;
    private const int ITEM = 77;

    private readonly LiveRoomHarness _room = new();
    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomDecorationTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
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
                Id = 1,
                Name = "room",
                PlayerEntityId = OWNER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                PaintWall = "101",
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
        RoomHarness.SetField(_room.Room, "_dbCtxFactory", _db);
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(FurnitureCategory.WallPaper, "205", RoomPropertyType.Wall, "wallpaper")]
    [InlineData(FurnitureCategory.Floor, "307", RoomPropertyType.Floor, "floor")]
    [InlineData(FurnitureCategory.Landscape, "1.3", RoomPropertyType.Landscape, "landscape")]
    public async Task TheOwner_PaintsTheRoomForEveryone_SavesIt_AndUsesTheItemUp(
        FurnitureCategory category,
        string pattern,
        RoomPropertyType property,
        string key
    )
    {
        Holds(OWNER, category, pattern);

        (await ApplyAsync(OWNER)).Should().BeTrue();

        Broadcast()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(new RoomPropertyMessageComposer { Key = key, Value = pattern });
        (await EntryPropertiesAsync())
            .Should()
            .Contain(new KeyValuePair<RoomPropertyType, string>(property, pattern));
        _room
            .Fakes.Log.Of("ConsumeFurnitureAsync")
            .Single()
            .Args[0]
            .Should()
            .Be((Turbo.Primitives.Rooms.Object.RoomObjectId)ITEM);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var saved = await db.Rooms.SingleAsync(Ct);

        (
            property switch
            {
                RoomPropertyType.Wall => saved.PaintWall,
                RoomPropertyType.Floor => saved.PaintFloor,
                _ => saved.PaintLandscape,
            }
        )
            .Should()
            .Be(pattern);
    }

    [Fact]
    public async Task AGuest_ChangesNothing_AndKeepsTheItem()
    {
        Holds(GUEST, FurnitureCategory.WallPaper, "205");

        (await ApplyAsync(GUEST)).Should().BeFalse();

        await NothingChangedAsync();
    }

    [Fact]
    public async Task FurniThatIsNoRoomPaper_ChangesNothing_AndIsKept()
    {
        Holds(OWNER, FurnitureCategory.Poster, "12");

        (await ApplyAsync(OWNER)).Should().BeFalse();

        await NothingChangedAsync();
    }

    [Fact]
    public async Task ThePacket_CarriesTheItemId_ToTheRoomThePlayerIsIn()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("RequestRoomPropertySetMessageEvent"),
            PacketHarness.Payload(w => w.Int(ITEM)),
            playerId: OWNER,
            roomId: 5
        );

        var call = harness.Fakes.Log.Of("ApplyDecorationAsync").Should().ContainSingle().Subject;

        call.Key.Should().Be(5L);
        ((ActionContext)call.Args[0]!)
            .PlayerId.Should()
            .Be((Turbo.Primitives.Players.PlayerId)OWNER);
        call.Args[1].Should().Be((Turbo.Primitives.Rooms.Object.RoomObjectId)ITEM);
    }

    [Fact]
    public void ThePacket_IsReadAsTheItemId() =>
        PacketHarness
            .Parse(
                PacketHarness.Incoming("RequestRoomPropertySetMessageEvent"),
                PacketHarness.Payload(w => w.Int(ITEM))
            )
            .Should()
            .BeEquivalentTo(new RequestRoomPropertySetMessage { ItemId = ITEM });

    private Task<bool> ApplyAsync(int playerId) =>
        _room.Room.ApplyDecorationAsync(ActionContext.CreateForPlayer(playerId, 1), ITEM, Ct);

    /// <summary>The player's inventory holds the paper, and gives it up when asked.</summary>
    private void Holds(int playerId, FurnitureCategory category, string pattern)
    {
        var item = new FurnitureItemSnapshot
        {
            ItemId = ITEM,
            SpriteId = 3001,
            OwnerId = playerId,
            OwnerName = "holder",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 3001,
                SpriteId = 3001,
                Name = "paper",
                ProductType = ProductType.Wall,
                FurniCategory = category,
                LogicName = "default_wall",
                TotalStates = 0,
                Width = 1,
                Length = 1,
                StackHeight = Turbo.Primitives.Rooms.Object.Altitude.Zero,
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = false,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Nobody,
                ExtraData = null,
            },
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = pattern },
            ExtraData = Turbo.Primitives.Furniture.ProductStuffData.ExtraData(pattern),
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };

        _room.Fakes.Handlers["GetItemSnapshotAsync"] = _ =>
            Task.FromResult<FurnitureItemSnapshot?>(item);
        _room.Fakes.Handlers["ConsumeFurnitureAsync"] = _ =>
            Task.FromResult<FurnitureItemSnapshot?>(item);
    }

    private IEnumerable<object> Broadcast() =>
        _room
            .Fakes.Log.Of("OnNextAsync")
            .SelectMany(c => ((RoomOutboundSnapshot)c.Args[0]!).Composers)
            .OfType<RoomPropertyMessageComposer>();

    /// <summary>The papers a player entering the room now is sent.</summary>
    private async Task<
        IEnumerable<KeyValuePair<RoomPropertyType, string>>
    > EntryPropertiesAsync() => (await _room.Room.GetEntryViewAsync(GUEST, Ct)).Properties;

    private async Task NothingChangedAsync()
    {
        _room.Fakes.Log.Of("ConsumeFurnitureAsync").Should().BeEmpty();
        Broadcast().Should().BeEmpty();
        (await EntryPropertiesAsync()).Should().BeEmpty();

        await using var db = await _db.CreateDbContextAsync(Ct);

        (await db.Rooms.SingleAsync(Ct)).PaintWall.Should().Be("101");
    }
}
