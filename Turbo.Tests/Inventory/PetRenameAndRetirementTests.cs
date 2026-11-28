using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Movements;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Grains;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// What client build 116 added to a player's pets: a pet in the inventory is renamed under the
/// rules a bought pet's name is held to, a pet is sent to retirement for the hotel's price and
/// gone for good, and the pets tab of a client that reads locations lists the pets standing in
/// rooms too, each with its room.
/// </summary>
public sealed class PetRenameAndRetirementTests : IDisposable
{
    private const int OWNER = 8;
    private const int OTHER = 9;
    private const int ROOM = 5;
    private const int DOG = 0;

    private readonly FurnitureWorld _world = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetRenameAndRetirementTests()
    {
        _world.AddPlayer(OWNER, "owner");
        _world.AddPlayer(OTHER, "other");
        _world.AddRoom(ROOM, OWNER);
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task A_pet_in_the_inventory_is_renamed_in_its_row_and_in_the_list()
    {
        var inventory = await InventoryAsync();
        var pet = await NewPetAsync(inventory, "Rex");

        (await inventory.RenamePetAsync(pet.Id, "  Fido  ", Ct)).Should().BeTrue();

        (await inventory.GetPetSnapshotAsync(pet.Id, Ct))!.Name.Should().Be("Fido");
        (await NameInDatabaseAsync(pet.Id)).Should().Be("Fido");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sixteen letters!")]
    [InlineData("Rex<3")]
    public async Task A_name_a_bought_pet_could_not_have_is_refused(string name)
    {
        var inventory = await InventoryAsync();
        var pet = await NewPetAsync(inventory, "Rex");

        (await inventory.RenamePetAsync(pet.Id, name, Ct)).Should().BeFalse();

        (await NameInDatabaseAsync(pet.Id)).Should().Be("Rex");
    }

    [Fact]
    public async Task A_name_holding_a_filtered_word_is_refused()
    {
        var inventory = await InventoryAsync();
        var pet = await NewPetAsync(inventory, "Rex");

        GrainHarness.Replace<IWordFilter>(inventory, new NothingIsCleanFilter());

        (await inventory.RenamePetAsync(pet.Id, "Fido", Ct)).Should().BeFalse();
        (await NameInDatabaseAsync(pet.Id)).Should().Be("Rex");
    }

    [Fact]
    public async Task Only_the_owners_inventory_renames_a_pet_and_only_one_that_is_in_it()
    {
        var inventory = await InventoryAsync();
        var kept = await NewPetAsync(inventory, "Rex");
        var placed = await NewPetAsync(inventory, "Luna");

        await inventory.TryCheckOutPetAsync(placed.Id, (RoomId)ROOM, TestCustody.Cause(), Ct);

        (await (await InventoryAsync(OTHER)).RenamePetAsync(kept.Id, "Mine", Ct))
            .Should()
            .BeFalse("it is someone else's pet");
        (await inventory.RenamePetAsync(placed.Id, "Nova", Ct))
            .Should()
            .BeFalse("a pet standing in a room is the room's to rename");

        (await NameInDatabaseAsync(kept.Id)).Should().Be("Rex");
        (await NameInDatabaseAsync(placed.Id)).Should().Be("Luna");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task The_retirement_price_is_the_hotels_setting(int price)
    {
        var inventory = await InventoryAsync(price: price);

        await inventory.SendPetRetirementPriceAsync(Ct);

        _world
            .Fakes.SentToPlayer<PetRetirementPriceMessageComposer>(OWNER)
            .Should()
            .ContainSingle()
            .Which.Credits.Should()
            .Be(price);
    }

    [Fact]
    public async Task A_free_retirement_deletes_the_pet_and_charges_nothing()
    {
        var inventory = await InventoryAsync();
        var pet = await NewPetAsync(inventory, "Rex");

        (await inventory.RetirePetAsync(pet.Id, Ct)).Should().BeTrue();

        (await NameInDatabaseAsync(pet.Id)).Should().BeNull("the pet is gone for good");
        (await inventory.GetPetSnapshotAsync(pet.Id, Ct)).Should().BeNull();
        _world.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty();
        _world
            .Fakes.SentToPlayer<PetRemovedFromInventoryEventMessageComposer>(OWNER)
            .Should()
            .ContainSingle()
            .Which.PetId.Should()
            .Be(pet.Id);
        _world
            .Fakes.SentToPlayer<PetRetirementResultMessageComposer>(OWNER)
            .Should()
            .Equal(new PetRetirementResultMessageComposer { PetId = pet.Id, Success = true });
        _world
            .Movements()
            .Where(x => x.Cause == MovementCauses.PET_RETIRE)
            .Select(x => (x.Seq, x.AssetId, x.FromKind, x.ToKind))
            .Should()
            .Equal(
                (
                    (short)0,
                    (long)pet.Id,
                    Turbo.Primitives.Movements.Enums.PlaceKind.Inventory,
                    Turbo.Primitives.Movements.Enums.PlaceKind.None
                )
            );
    }

    [Fact]
    public async Task A_priced_retirement_charges_the_credits_before_the_pet_goes()
    {
        _world.Fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());

        var inventory = await InventoryAsync(price: 25);
        var pet = await NewPetAsync(inventory, "Rex");

        (await inventory.RetirePetAsync(pet.Id, Ct)).Should().BeTrue();

        var debit = _world.Fakes.Log.Of("TryDebitAsync").Should().ContainSingle().Subject;
        var charged = ((List<WalletDebitRequest>)debit.Args[0]!).Should().ContainSingle().Subject;

        debit.Key.Should().Be((long)OWNER);
        charged.CurrencyKind.CurrencyType.Should().Be(CurrencyType.Credits);
        charged.Amount.Should().Be(25);
        ((MovementCause)debit.Args[1]!).Cause.Should().Be(MovementCauses.PET_RETIRE);
        (await NameInDatabaseAsync(pet.Id)).Should().BeNull();
        _world
            .Movements()
            .Where(x => x.Cause == MovementCauses.PET_RETIRE)
            .Select(x => x.Seq)
            .Should()
            .Equal([(short)1], "the pet's movement follows the charge's");
    }

    [Fact]
    public async Task A_player_who_cannot_pay_keeps_the_pet()
    {
        _world.Fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Failed());

        var inventory = await InventoryAsync(price: 25);
        var pet = await NewPetAsync(inventory, "Rex");

        (await inventory.RetirePetAsync(pet.Id, Ct)).Should().BeFalse();

        (await NameInDatabaseAsync(pet.Id)).Should().Be("Rex");
        _world
            .Fakes.SentToPlayer<PetRetirementResultMessageComposer>(OWNER)
            .Should()
            .Equal(new PetRetirementResultMessageComposer { PetId = pet.Id, Success = false });
        _world
            .Fakes.SentToPlayer<PetRemovedFromInventoryEventMessageComposer>(OWNER)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task A_pet_standing_in_a_room_or_someone_elses_is_not_retired_nor_charged_for()
    {
        _world.Fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());

        var inventory = await InventoryAsync(price: 25);
        var placed = await NewPetAsync(inventory, "Luna");
        var kept = await NewPetAsync(inventory, "Rex");

        await inventory.TryCheckOutPetAsync(placed.Id, (RoomId)ROOM, TestCustody.Cause(), Ct);

        (await inventory.RetirePetAsync(placed.Id, Ct)).Should().BeFalse();
        (await (await InventoryAsync(OTHER, price: 25)).RetirePetAsync(kept.Id, Ct))
            .Should()
            .BeFalse();

        _world.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty();
        (await NameInDatabaseAsync(placed.Id)).Should().Be("Luna");
        (await NameInDatabaseAsync(kept.Id)).Should().Be("Rex");
        _world
            .Fakes.SentToPlayer<PetRetirementResultMessageComposer>(OWNER)
            .Should()
            .Equal(new PetRetirementResultMessageComposer { PetId = placed.Id, Success = false });
    }

    [Fact]
    public async Task The_plain_pets_tab_lists_only_the_pets_in_the_inventory()
    {
        var inventory = await InventoryAsync();
        var kept = await NewPetAsync(inventory, "Rex");
        var placed = await NewPetAsync(inventory, "Luna");

        await inventory.TryCheckOutPetAsync(placed.Id, (RoomId)ROOM, TestCustody.Cause(), Ct);
        await inventory.SendPetInventoryAsync(Ct);

        var list = _world
            .Fakes.SentToPlayer<PetInventoryEventMessageComposer>(OWNER)
            .Should()
            .ContainSingle()
            .Subject;

        list.Pets.Select(x => x.Id).Should().Equal(kept.Id);
        list.Rooms.Should().BeEmpty();
        _world.Fakes.Log.Of("GetRoomsByIdsAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task The_pets_tab_with_locations_lists_a_placed_pet_with_its_room()
    {
        var inventory = await InventoryAsync();
        var kept = await NewPetAsync(inventory, "Rex");
        var placed = await NewPetAsync(inventory, "Luna");
        var alsoKept = await NewPetAsync(inventory, "Milo");

        await inventory.TryCheckOutPetAsync(placed.Id, (RoomId)ROOM, TestCustody.Cause(), Ct);
        await inventory.SendPetInventoryWithLocationsAsync(Ct);

        var list = _world
            .Fakes.SentToPlayer<PetInventoryEventMessageComposer>(OWNER)
            .Should()
            .ContainSingle()
            .Subject;

        list.TotalFragments.Should().Be(1);
        list.Pets.Select(x => (x.Id, x.RoomId))
            .Should()
            .Equal(
                (kept.Id, (RoomId?)null),
                (placed.Id, (RoomId?)new RoomId(ROOM)),
                (alsoKept.Id, (RoomId?)null)
            );
        list.Rooms.Should()
            .Equal(
                new PetRoomSnapshot
                {
                    RoomId = new RoomId(ROOM),
                    Name = "Garden",
                    Description = "Where Luna lives",
                }
            );
        ((IReadOnlyCollection<RoomId>)_world.Fakes.Log.Of("GetRoomsByIdsAsync").Single().Args[0]!)
            .Should()
            .Equal(new RoomId(ROOM));
    }

    [Fact]
    public async Task Someone_elses_pet_in_the_players_room_is_not_in_their_tab()
    {
        var others = await InventoryAsync(OTHER);
        var guest = await NewPetAsync(others, "Guest");

        await others.TryCheckOutPetAsync(guest.Id, (RoomId)ROOM, TestCustody.Cause(), Ct);
        await (await InventoryAsync()).SendPetInventoryWithLocationsAsync(Ct);

        _world
            .Fakes.SentToPlayer<PetInventoryEventMessageComposer>(OWNER)
            .Should()
            .ContainSingle()
            .Which.Pets.Should()
            .BeEmpty();
    }

    private static Task<PetSnapshot> NewPetAsync(IInventoryGrain inventory, string name) =>
        inventory.CreatePetAsync(name, DOG, 1, 1, "FFFFFF", 0, [], TestCustody.Cause(), Ct)!;

    private async Task<string?> NameInDatabaseAsync(int petId)
    {
        await using var db = await _world.Db.CreateDbContextAsync(Ct);

        return await db
            .Pets.AsNoTracking()
            .Where(x => x.Id == petId)
            .Select(x => x.Name)
            .SingleOrDefaultAsync(Ct);
    }

    /// <summary>
    /// An inventory grain with its real pet module, recording through the real recorder, whose
    /// navigator knows the room as "Garden".
    /// </summary>
    private async Task<IInventoryGrain> InventoryAsync(int playerId = OWNER, int price = 0)
    {
        var grain = GrainHarness.Create<InventoryGrain>(_world.Fakes, _world.Db, playerId);
        var state = GrainHarness.Get<InventoryLiveState>(grain);

        _world.Fakes.Handlers["GetRoomsByIdsAsync"] = call =>
            Task.FromResult(
                ((IReadOnlyCollection<RoomId>)call.Args[0]!).ToImmutableDictionary(
                    x => x,
                    x =>
                        (
                            (RoomInfoSnapshot)
                                RuntimeHelpers.GetUninitializedObject(typeof(RoomInfoSnapshot))
                        ) with
                        {
                            RoomId = x,
                            Name = "Garden",
                            Description = "Where Luna lives",
                        }
                )
            );

        GrainHarness.Replace(grain, new InventoryConfig { PetRetirementCreditsPrice = price });
        GrainHarness.Replace(grain, _world.Fakes.Create<INavigatorService>());
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

        return (IInventoryGrain)grain;
    }

    /// <summary>A hotel filter that finds a forbidden word in everything.</summary>
    private sealed class NothingIsCleanFilter : IWordFilter
    {
        public string Replacement => "bobba";

        public string Filter(string text) => Replacement;

        public string Filter(string text, IReadOnlySet<string> extraWords) => Replacement;

        public bool IsClean(string text) => false;

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
