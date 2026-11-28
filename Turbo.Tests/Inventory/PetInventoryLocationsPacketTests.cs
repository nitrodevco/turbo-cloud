using FluentAssertions;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Extensions;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Features;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// Client build 116's <c>PetData</c> ends with where the pet is: the id of its room, -1 for the
/// inventory, and only for a room its name and description. One parser reads it in four
/// packets, so a client naming <c>pet_inventory_locations</c> is written all four that way and
/// any other client exactly what it was written before. The same build asks for a pet's
/// retirement and a new name in packets of its own.
/// </summary>
public sealed class PetInventoryLocationsPacketTests
{
    private const int PLAYER = 1;
    private const int ROOM = 77;
    private const int KEPT = 4;
    private const int PLACED = 5;

    private static readonly PetRoomSnapshot Garden = new()
    {
        RoomId = new RoomId(ROOM),
        Name = "Garden",
        Description = "Where Luna lives",
    };

    [Fact]
    public void An_older_clients_list_holds_the_inventorys_pets_in_the_layout_it_always_had()
    {
        var plain = Write(Plain<PetInventoryEventMessageComposer>(), List(Pet(KEPT, null)));
        var withPlaced = Write(
            Plain<PetInventoryEventMessageComposer>(),
            List(Pet(KEPT, null), Pet(PLACED, ROOM))
        );

        withPlaced
            .ToArray()
            .Should()
            .Equal(plain.ToArray(), "a pet standing in a room is not in its list");

        var packet = Read(plain);

        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(1);
        ReadPet(packet).Should().Be(KEPT);
        packet.End.Should().BeTrue("the pet ends at its rarity level");
    }

    [Fact]
    public void A_client_with_the_feature_reads_every_pet_with_where_it_is()
    {
        var packet = Read(
            Write(
                Feature<PetInventoryEventMessageComposer>(),
                List(Pet(KEPT, null), Pet(PLACED, ROOM))
            )
        );

        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(2);

        ReadPet(packet).Should().Be(KEPT);
        packet.PopInt().Should().Be(-1, "in the inventory, and no room texts follow");

        ReadPet(packet).Should().Be(PLACED);
        packet.PopInt().Should().Be(ROOM);
        packet.PopString().Should().Be("Garden");
        packet.PopString().Should().Be("Where Luna lives");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void A_placed_pet_whose_room_is_not_known_still_reads_as_placed()
    {
        var composer = List(Pet(PLACED, ROOM)) with { Rooms = [] };
        var packet = Read(Write(Feature<PetInventoryEventMessageComposer>(), composer));

        packet.PopInt();
        packet.PopInt();
        packet.PopInt().Should().Be(1);
        ReadPet(packet).Should().Be(PLACED);
        packet.PopInt().Should().Be(ROOM);
        packet.PopString().Should().BeEmpty();
        packet.PopString().Should().BeEmpty();
        packet.End.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_pet_joining_the_inventory_ends_with_no_room_only_for_the_feature(bool feature)
    {
        var composer = new PetAddedToInventoryEventMessageComposer
        {
            Pet = Pet(KEPT, null),
            OpenInventory = true,
        };
        var packet = Read(
            Write(Serializer<PetAddedToInventoryEventMessageComposer>(feature), composer)
        );

        ReadPet(packet).Should().Be(KEPT);

        if (feature)
            packet.PopInt().Should().Be(-1);

        packet.PopBoolean().Should().BeTrue();
        packet.End.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_bought_pet_ends_with_no_room_only_for_the_feature(bool feature)
    {
        var composer = new PetReceivedMessageComposer
        {
            BoughtAsGift = false,
            Pet = Pet(KEPT, null),
        };
        var packet = Read(Write(Serializer<PetReceivedMessageComposer>(feature), composer));

        packet.PopBoolean().Should().BeFalse();
        ReadPet(packet).Should().Be(KEPT);

        if (feature)
            packet.PopInt().Should().Be(-1);

        packet.End.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_respected_pet_ends_with_its_room_only_for_the_feature(bool feature)
    {
        var composer = new PetRespectNotificationEventMessageComposer
        {
            Respect = 3,
            PetOwnerId = PLAYER,
            Pet = Pet(PLACED, ROOM),
            Room = Garden,
        };
        var packet = Read(
            Write(Serializer<PetRespectNotificationEventMessageComposer>(feature), composer)
        );

        packet.PopInt().Should().Be(3);
        packet.PopInt().Should().Be(PLAYER);
        ReadPet(packet).Should().Be(PLACED);

        if (feature)
        {
            packet.PopInt().Should().Be(ROOM);
            packet.PopString().Should().Be("Garden");
            packet.PopString().Should().Be("Where Luna lives");
        }

        packet.End.Should().BeTrue();
    }

    [Fact]
    public void The_feature_changes_the_four_packets_that_carry_a_pet_and_no_other()
    {
        PacketHarness
            .Revision.FeatureSerializers[ClientFeatures.PET_INVENTORY_LOCATIONS]
            .Keys.Should()
            .BeEquivalentTo([
                typeof(PetInventoryEventMessageComposer),
                typeof(PetAddedToInventoryEventMessageComposer),
                typeof(PetReceivedMessageComposer),
                typeof(PetRespectNotificationEventMessageComposer),
            ]);

        foreach (
            var (type, serializer) in PacketHarness.Revision.FeatureSerializers[
                ClientFeatures.PET_INVENTORY_LOCATIONS
            ]
        )
            serializer.Header.Should().Be(PacketHarness.Revision.Serializers[type].Header);
    }

    [Theory]
    [InlineData(false, nameof(IInventoryGrain.SendPetInventoryAsync))]
    [InlineData(true, nameof(IInventoryGrain.SendPetInventoryWithLocationsAsync))]
    public async Task The_pets_tab_is_asked_for_with_locations_only_by_a_client_that_reads_them(
        bool feature,
        string asked
    )
    {
        var harness = new PacketHarness();

        if (feature)
            harness.Fakes.Handlers["get_ClientFeatures"] = _ =>
                ClientFeatures.Known([ClientFeatures.PET_INVENTORY_LOCATIONS]);

        await harness.SendAsync(PacketHarness.Incoming("GetPetInventoryMessageEvent"), []);

        harness
            .Fakes.Log.Calls.Where(x => x.Interface == typeof(IInventoryGrain))
            .Select(x => x.Method)
            .Should()
            .Equal(asked);
    }

    [Fact]
    public void The_new_packets_have_the_headers_and_layouts_the_client_writes_and_reads()
    {
        PacketHarness.Incoming("GetPetRetirementPriceMessageEvent").Should().Be(7700);
        PacketHarness.Incoming("RetirePetMessageEvent").Should().Be(7701);
        PacketHarness.Incoming("RenamePetMessageEvent").Should().Be(7702);

        PacketHarness.Parse(7700, []).Should().BeOfType<GetPetRetirementPriceMessage>();
        PacketHarness
            .Parse(7701, PacketHarness.Payload(w => w.Int(KEPT)))
            .Should()
            .Be(new RetirePetMessage { PetId = KEPT });
        PacketHarness
            .Parse(7702, PacketHarness.Payload(w => w.Int(KEPT).String("Fido")))
            .Should()
            .Be(new RenamePetMessage { PetId = KEPT, Name = "Fido" });

        var price = PacketHarness.Encode(new PetRetirementPriceMessageComposer { Credits = 25 });

        price.Header.Should().Be(7700);
        price.PopInt().Should().Be(25);
        price.End.Should().BeTrue();

        var retired = PacketHarness.Encode(
            new PetRetirementResultMessageComposer { PetId = KEPT, Success = true }
        );

        retired.Header.Should().Be(7701);
        retired.PopInt().Should().Be(KEPT);
        retired.PopBoolean().Should().BeTrue();
        retired.End.Should().BeTrue();

        var renamed = PacketHarness.Encode(
            new PetRenameResultMessageComposer { PetId = KEPT, Success = false }
        );

        renamed.Header.Should().Be(7702);
        renamed.PopInt().Should().Be(KEPT);
        renamed.PopBoolean().Should().BeFalse();
        renamed.End.Should().BeTrue();
    }

    [Fact]
    public async Task The_retirement_packets_reach_the_players_inventory()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(7700, []);
        await harness.SendAsync(7701, PacketHarness.Payload(w => w.Int(KEPT)));

        harness
            .Fakes.Log.Of(nameof(IInventoryGrain.SendPetRetirementPriceAsync))
            .Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be((long)PLAYER);
        harness
            .Fakes.Log.Of(nameof(IInventoryGrain.RetirePetAsync))
            .Should()
            .ContainSingle()
            .Which.Args[0]
            .Should()
            .Be(KEPT);
    }

    /// <summary>
    /// A pet standing in the room the player is in is the room's to rename; any other is asked
    /// of the inventory. The dialog waits for its answer, so it is answered either way.
    /// </summary>
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public async Task A_rename_is_asked_of_the_room_then_the_inventory_and_always_answered(
        bool inRoom,
        bool roomRenames,
        bool inventoryRenames,
        bool success
    )
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers[nameof(IRoomGrain.RenamePetAsync)] = call =>
            call.Interface == typeof(IRoomGrain)
                ? Task.FromResult(roomRenames)
                : Task.FromResult(inventoryRenames);

        var replies = await harness.SendAsync(
            7702,
            PacketHarness.Payload(w => w.Int(KEPT).String("Fido")),
            PLAYER,
            inRoom ? ROOM : -1
        );

        var asked = harness.Fakes.Log.Of(nameof(IRoomGrain.RenamePetAsync));

        asked
            .Select(x => x.Interface)
            .Should()
            .Equal(
                (inRoom, roomRenames) switch
                {
                    (true, true) => [typeof(IRoomGrain)],
                    (true, false) => [typeof(IRoomGrain), typeof(IInventoryGrain)],
                    _ => [typeof(IInventoryGrain)],
                }
            );
        asked.Should().OnlyContain(x => x.Args.Contains(KEPT) && x.Args.Contains("Fido"));

        var reply = replies.Should().ContainSingle().Subject;

        reply.Header.Should().Be(7702);
        reply.PopInt().Should().Be(KEPT);
        reply.PopBoolean().Should().Be(success);
    }

    private static PetInventoryEventMessageComposer List(params PetSnapshot[] pets) =>
        new()
        {
            TotalFragments = 1,
            CurrentFragment = 0,
            Pets = [.. pets],
            Rooms = pets.Any(x => x.RoomId is not null) ? [Garden] : [],
        };

    private static ISerializer Serializer<T>(bool feature) => feature ? Feature<T>() : Plain<T>();

    private static ISerializer Plain<T>() => PacketHarness.Revision.Serializers[typeof(T)];

    private static ISerializer Feature<T>() =>
        PacketHarness.Revision.FeatureSerializers[ClientFeatures.PET_INVENTORY_LOCATIONS][
            typeof(T)
        ];

    /// <summary>The payload a serializer writes, without its frame (length and header).</summary>
    private static ReadOnlyMemory<byte> Write(ISerializer serializer, IComposer composer)
    {
        using var written = serializer.Serialize(composer);

        return written.ToArray().AsMemory(6);
    }

    private static ClientPacket Read(ReadOnlyMemory<byte> payload) => new(0, payload);

    /// <summary>PetData up to where the older layout ends: id, name, the figure, level, rarity.</summary>
    private static int ReadPet(ClientPacket packet)
    {
        var id = packet.PopInt();

        packet.PopString();
        packet.PopInt();
        packet.PopInt();
        packet.PopString();
        packet.PopInt();
        packet.PopInt().Should().Be(0);
        packet.PopInt();
        packet.PopInt();

        return id;
    }

    private static PetSnapshot Pet(int id, int? roomId) =>
        new PetEntity
        {
            Id = id,
            PlayerEntityId = PLAYER,
            RoomEntityId = roomId,
            Name = $"pet-{id}",
            TypeId = 0,
            PaletteId = 1,
            BreedId = 1,
            Color = "FFFFFF",
            RarityLevel = 0,
        }.ToSnapshot("owner");
}
