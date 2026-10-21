using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Movements;
using Turbo.Inventory.Factories;
using Turbo.Inventory.Grains;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// Pets and bots in the custody record (docs/movements.md "Pets and bots"): while recording, a
/// pet or bot made, handed to a room, taken back and deleted is one movement each under the
/// caller's cause, written with its row; with recording off the rows change as they did and
/// nothing is recorded.
/// </summary>
public sealed class PetBotMovementTests : IDisposable
{
    private const int OWNER = 8;
    private const int ROOM = 5;
    private const int DOG = 0;

    private readonly FurnitureWorld _world = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetBotMovementTests()
    {
        _world.AddPlayer(OWNER, "owner");
        _world.AddRoom(ROOM, OWNER);
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task APetsLife_IsMadePlacedTakenBackAndDeleted_OneMovementEach()
    {
        var inventory = await InventoryAsync();
        var made = MovementCause.New(MovementCauses.CATALOG_PURCHASE, OWNER, 1).StartingAt(1);
        var placed = MovementCause.New(MovementCauses.PET_PLACE, OWNER, 1);
        var picked = MovementCause.New(MovementCauses.PET_PICKUP, OWNER, 1);
        var composted = MovementCause.New(MovementCauses.PET_COMPOST, OWNER, 1);

        var pet = (await inventory.CreatePetAsync("rex", 3, 1, 1, "FFFFFF", 0, [], made, Ct))!;
        var inRoom = (await inventory.TryCheckOutPetAsync(pet.Id, (RoomId)ROOM, placed, Ct))!;
        (await inventory.ReturnPetAsync(inRoom, picked, Ct)).Should().BeTrue();
        (await inventory.DeletePetAsync(pet.Id, composted, Ct)).Should().BeTrue();

        _world
            .Movements()
            .Select(x =>
                (
                    x.Cause,
                    x.Seq,
                    x.AssetKind,
                    x.AssetId,
                    x.DefinitionId,
                    x.FromKind,
                    x.FromPlace,
                    x.ToKind,
                    x.ToPlace
                )
            )
            .Should()
            .Equal(
                (
                    MovementCauses.CATALOG_PURCHASE,
                    (short)1,
                    AssetKind.Pet,
                    (long)pet.Id,
                    3,
                    PlaceKind.None,
                    0L,
                    PlaceKind.Inventory,
                    0L
                ),
                (
                    MovementCauses.PET_PLACE,
                    (short)0,
                    AssetKind.Pet,
                    (long)pet.Id,
                    3,
                    PlaceKind.Inventory,
                    0L,
                    PlaceKind.Room,
                    (long)ROOM
                ),
                (
                    MovementCauses.PET_PICKUP,
                    (short)0,
                    AssetKind.Pet,
                    (long)pet.Id,
                    3,
                    PlaceKind.Room,
                    (long)ROOM,
                    PlaceKind.Inventory,
                    0L
                ),
                (
                    MovementCauses.PET_COMPOST,
                    (short)0,
                    AssetKind.Pet,
                    (long)pet.Id,
                    3,
                    PlaceKind.Inventory,
                    0L,
                    PlaceKind.None,
                    0L
                )
            );
        _world
            .Movements()
            .Should()
            .AllSatisfy(x => x.ToOwner.Should().Be(x.ToKind == PlaceKind.None ? 0 : OWNER));
    }

    [Fact]
    public async Task ABotHandedToARoom_IsOneMovement_AndARefusedHandOverNone()
    {
        var inventory = await InventoryAsync();

        var bot = (
            await inventory.CreateBotAsync(
                "bob",
                "",
                "hd-180-1",
                AvatarGenderType.Male,
                TestCustody.Cause(),
                Ct
            )
        )!;
        (await inventory.TryCheckOutBotAsync(bot.Id, (RoomId)ROOM, TestCustody.Cause(), Ct))
            .Should()
            .NotBeNull();
        (await inventory.TryCheckOutBotAsync(bot.Id, (RoomId)ROOM, TestCustody.Cause(), Ct))
            .Should()
            .BeNull("it is no longer in the inventory");

        _world
            .Movements()
            .Select(x => (x.AssetKind, x.DefinitionId, x.FromKind, x.ToKind))
            .Should()
            .Equal(
                (AssetKind.Bot, 0, PlaceKind.None, PlaceKind.Inventory),
                (AssetKind.Bot, 0, PlaceKind.Inventory, PlaceKind.Room)
            );
    }

    [Fact]
    public async Task WithRecordingOff_PetsMoveAsBefore_AndNothingIsRecorded()
    {
        _world.Config.Set(new() { Enabled = false });
        var inventory = await InventoryAsync();

        var pet = (
            await inventory.CreatePetAsync("rex", 3, 1, 1, "FFFFFF", 0, [], TestCustody.Cause(), Ct)
        )!;
        await inventory.TryCheckOutPetAsync(pet.Id, (RoomId)ROOM, TestCustody.Cause(), Ct);

        await using (var db = await _world.Db.CreateDbContextAsync(Ct))
            (await db.Pets.AsNoTracking().SingleAsync(Ct)).RoomEntityId.Should().Be(ROOM);

        (await inventory.DeletePetAsync(pet.Id, TestCustody.Cause(), Ct)).Should().BeTrue();

        await using (var db = await _world.Db.CreateDbContextAsync(Ct))
            db.Pets.Should().BeEmpty();

        _world.Movements().Should().BeEmpty();
    }

    /// <summary>An inventory grain with its real pet and bot modules, recording through the real recorder.</summary>
    private async Task<IInventoryGrain> InventoryAsync()
    {
        var grain = GrainHarness.Create<InventoryGrain>(_world.Fakes, _world.Db, OWNER);
        var state = GrainHarness.Get<InventoryLiveState>(grain);

        GrainHarness.Replace<IAssetMovementRecorder>(grain, await _world.NewRecorderAsync(Ct));
        GrainHarness.Replace(
            grain,
            new InventoryPetModule(
                grain,
                state,
                _world.Db,
                _world.Fakes.Create<IPetBreedProvider>(),
                NullLogger.Instance
            )
        );
        GrainHarness.Replace(
            grain,
            new InventoryBotModule(grain, state, _world.Db, NullLogger.Instance)
        );

        return (IInventoryGrain)grain;
    }
}
