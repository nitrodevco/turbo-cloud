using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Bots;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Furniture.Custody;
using Turbo.Primitives.Action;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room deleted with pets in it hands each pet back to its owner. One the owner's inventory
/// refuses (its row is gone or no longer theirs), or whose return throws (the inventory's write
/// failed, the call timed out), must not stop the deletion halfway: the other pets still go home,
/// the room is still deleted, and the database backstop takes the failed one out of the room.
/// Bots and furni are handed home the same way.
/// </summary>
public sealed class RoomDeletionPetReturnTests : IDisposable
{
    private const int OWNER = 1;
    private const int REFUSED = 40;
    private const int RETURNED = 41;
    private const int THROWN = 42;
    private const int BOT_THROWN = 50;
    private const int BOT_RETURNED = 51;
    private const int ITEM = 60;
    private const int CAT = 1;

    // A wired room is a live room that can also stand a floor furni in it.
    private readonly WiredRoom _room = new(10, 10);
    private readonly LiveRoomHarness _harness;
    private readonly SqliteDb _db = new();
    private readonly CapturingLogger<IRoomGrain> _log = new();
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomDeletionPetReturnTests()
    {
        _harness = _room.Harness;

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
        _db.Insert(PetRow(REFUSED));
        _db.Insert(PetRow(RETURNED));
        _db.Insert(PetRow(THROWN));
        _db.Insert(BotRow(BOT_THROWN));
        _db.Insert(BotRow(BOT_RETURNED));

        _harness.Room.UseDatabase(_db);
        _harness.Room.UseLogger(_log);
        _harness.Room.UseAvatarProvider(new RoomAvatarProvider(Options.Create(new BotConfig())));
        _harness.State.RoomSnapshot = _harness.State.RoomSnapshot with { AllowPets = true };

        _owner.SetPosition(2, 2);
        _harness.State.AvatarsByObjectId[5] = _owner;
        _harness.State.AvatarsByPlayerId[OWNER] = 5;
        _harness.Module<RoomMapModule>().AddAvatar(_owner, false);

        _harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = call =>
            Task.FromResult<PetSnapshot?>(Pet((int)call.Args[0]!));
        _harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnPetAsync)] = call =>
            Task.FromResult(((PetSnapshot)call.Args[0]!).Id != REFUSED);
    }

    public void Dispose() => _db.Dispose();

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    [Fact]
    public async Task A_pet_its_owner_cannot_take_back_does_not_stop_the_room_being_deleted()
    {
        var pets = _harness.Module<RoomPetModule>();
        (await pets.PlacePetAsync(Owner, REFUSED, 4, 4, Ct)).Should().BeTrue();
        (await pets.PlacePetAsync(Owner, RETURNED, 6, 6, Ct)).Should().BeTrue();

        (await _harness.Room.PrepareRoomDeletionAsync(Owner, Ct)).Should().NotBeNull();

        var deletion = () => _harness.Room.CompleteRoomDeletionAsync(Ct);

        await deletion.Should().NotThrowAsync();

        _harness
            .Fakes.Log.Of(nameof(IInventoryGrain.ReturnPetAsync))
            .Select(x => ((PetSnapshot)x.Args[0]!).Id)
            .Should()
            .Contain(RETURNED, "the other pet still goes home");
        pets.TryGetPet(REFUSED, out _).Should().BeFalse();
        pets.TryGetPet(RETURNED, out _).Should().BeFalse();
        _harness
            .Fakes.Log.Of(nameof(IRoomDirectoryGrain.RemoveActiveRoomAsync))
            .Should()
            .ContainSingle("the deletion ran to the end");

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Rooms.AnyAsync(x => x.Id == 1, Ct)).Should().BeFalse();
        (await db.Pets.SingleAsync(x => x.Id == REFUSED, Ct))
            .RoomEntityId.Should()
            .BeNull("the backstop takes the refused pet out of the deleted room");
        _log.AtLeast(LogLevel.Warning)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain($"Pet {REFUSED}");
    }

    [Fact]
    public async Task A_return_home_that_throws_does_not_stop_the_room_being_deleted()
    {
        _harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnPetAsync)] = call =>
            ((PetSnapshot)call.Args[0]!).Id == THROWN
                ? Task.FromException<bool>(new TimeoutException("inventory write failed"))
                : Task.FromResult(true);
        _harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutBotAsync)] = call =>
            Task.FromResult<BotSnapshot?>(Bot((int)call.Args[0]!));
        _harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnBotAsync)] = call =>
            ((BotSnapshot)call.Args[0]!).Id == BOT_THROWN
                ? Task.FromException<bool>(new TimeoutException("inventory write failed"))
                : Task.FromResult(true);
        _harness.Fakes.Handlers[nameof(IRoomPersistenceGrain.WritePickupAsync)] = _ =>
            Task.FromException(new TimeoutException("persistence call timed out"));

        // The ones that throw go first, so each owner's other unit is handed over after them.
        var pets = _harness.Module<RoomPetModule>();
        (await pets.PlacePetAsync(Owner, THROWN, 4, 4, Ct)).Should().BeTrue();
        (await pets.PlacePetAsync(Owner, RETURNED, 6, 6, Ct)).Should().BeTrue();
        (await _harness.Room.PlaceBotAsync(Owner, BOT_THROWN, 4, 6, Ct)).Should().BeTrue();
        (await _harness.Room.PlaceBotAsync(Owner, BOT_RETURNED, 6, 4, Ct)).Should().BeTrue();
        _room.AddFloorItem(ITEM, 8, 8);

        (await _harness.Room.PrepareRoomDeletionAsync(Owner, Ct)).Should().NotBeNull();

        var deletion = () => _harness.Room.CompleteRoomDeletionAsync(Ct);

        await deletion.Should().NotThrowAsync();

        _harness
            .Fakes.Log.Of(nameof(IInventoryGrain.ReturnPetAsync))
            .Select(x => ((PetSnapshot)x.Args[0]!).Id)
            .Should()
            .Contain(RETURNED, "the other pet still goes home");
        _harness
            .Fakes.Log.Of(nameof(IInventoryGrain.ReturnBotAsync))
            .Select(x => ((BotSnapshot)x.Args[0]!).Id)
            .Should()
            .Contain(BOT_RETURNED, "the other bot still goes home");
        _harness.State.Pets.Should().BeEmpty();
        _harness.State.Bots.Should().BeEmpty();
        _harness
            .Fakes.Log.Of(nameof(IRoomDirectoryGrain.RemoveActiveRoomAsync))
            .Should()
            .ContainSingle("the deletion ran to the end");
        _harness
            .Fakes.Log.Of(nameof(IFurnitureCustody.SendHomeLeftInRoomAsync))
            .Should()
            .ContainSingle("the backstop sends home the furni whose hand-over threw");

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Rooms.AnyAsync(x => x.Id == 1, Ct)).Should().BeFalse();
        (await db.Pets.SingleAsync(x => x.Id == THROWN, Ct))
            .RoomEntityId.Should()
            .BeNull("the backstop takes the pet whose return threw out of the deleted room");
        (await db.Bots.SingleAsync(x => x.Id == BOT_THROWN, Ct))
            .RoomEntityId.Should()
            .BeNull("the backstop takes the bot whose return threw out of the deleted room");

        var errors = _log.AtLeast(LogLevel.Error).ToList();
        errors.Should().HaveCount(3).And.OnlyContain(x => x.Exception is TimeoutException);
        errors
            .Select(x => x.Message)
            .Should()
            .Contain(x => x.Contains($"Pet {THROWN}") && x.Contains($"player {OWNER}"))
            .And.Contain(x => x.Contains($"Bot {BOT_THROWN}") && x.Contains($"player {OWNER}"))
            .And.Contain(x => x.Contains("Furniture of room 1"));
    }

    private static PetEntity PetRow(int id) =>
        new()
        {
            Id = id,
            PlayerEntityId = OWNER,
            RoomEntityId = 1,
            Name = "Rex",
            TypeId = CAT,
            PaletteId = 0,
            BreedId = 0,
            Color = "FFFFFF",
        };

    private static BotEntity BotRow(int id) =>
        new()
        {
            Id = id,
            PlayerEntityId = OWNER,
            RoomEntityId = 1,
            Name = "Bot",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
        };

    private static BotSnapshot Bot(int id) =>
        new()
        {
            Id = id,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = null,
            Name = "Bot",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            X = 0,
            Y = 0,
            Z = Altitude.Zero,
            Rotation = Rotation.South,
            FreeRoam = false,
            ChatText = string.Empty,
            AutoChat = false,
            ChatDelaySeconds = 0,
            MixSentences = false,
            DanceType = AvatarDanceType.None,
        };

    private static PetSnapshot Pet(int id) =>
        new()
        {
            Id = id,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = null,
            Name = "Rex",
            Figure = new PetFigureSnapshot
            {
                TypeId = CAT,
                PaletteId = 0,
                Color = "FFFFFF",
                BreedId = 0,
                CustomParts = [],
            },
            Level = 10,
            Experience = 0,
            Energy = 100,
            Nutrition = 100,
            Respect = 0,
            RarityLevel = 0,
            HasSaddle = false,
            AnyoneCanRide = false,
            HasBreedingPermission = false,
            X = 0,
            Y = 0,
            Z = Altitude.Zero,
            Rotation = Rotation.South,
            CreatedAtUtc = DateTime.UtcNow,
            WateredAtUtc = DateTime.UtcNow,
            HarvestedAtUtc = null,
        };
}
