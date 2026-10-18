using FluentAssertions;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "WIRED Room Linker" (<c>wf_room_linker</c>, Wired Faculty tutorial 30/04/2025): bought as a
/// linked pair like a teleporter, and a room linker whatever its definition's logic column says.
/// On the test hotel its definition named no logic, so a bought linker was one unlinked furni
/// and "Teleport to Room" had nowhere to send anyone. The live hotel tells the client each
/// half's partner in the item's extra number (official client capture 2026-10-18: ObjectAdd of
/// 924611506 with extra 924611507).
/// </summary>
public sealed class WiredRoomLinkerTests
{
    [Theory]
    [InlineData("teleport", "door_tele", true)]
    [InlineData("default_floor", "wf_room_linker", true)]
    [InlineData("wf_room_linker", "wf_room_linker", true)]
    [InlineData("default_floor", "chair_basic", false)]
    public void Linkers_and_teleporters_are_bought_as_pairs(
        string logicName,
        string classname,
        bool pair
    ) => TeleportFurniture.IsLinkedPair(logicName, classname).Should().Be(pair);

    [Fact]
    public void A_room_linker_gets_the_room_linker_logic()
    {
        var room = new WiredRoom();
        var item = room.AddFloorItem(40, 1, 1, TeleportFurniture.ROOM_LINKER_CLASSNAME);

        item.GetType()
            .GetProperty(nameof(item.Definition))!
            .SetValue(item, item.Definition with { LogicName = "default_floor" });

        room.Harness.LogicProvider.CreateLogicInstance(
                "default_floor",
                new RoomFloorItemContext(room.Harness.Room, item)
            )
            .Should()
            .BeOfType<FurnitureRoomLinkerLogic>();
    }

    [Fact]
    public void A_placed_linker_tells_the_client_its_partner()
    {
        var room = new WiredRoom();
        var item = room.AddFloorItem(
            40,
            1,
            1,
            TeleportFurniture.ROOM_LINKER_CLASSNAME,
            createLogic: (stuffData, ctx) => new FurnitureRoomLinkerLogic(stuffData, ctx)
        );

        item.SetExtraData(TeleportFurniture.PairExtraData(41));

        ((RoomFloorItemSnapshot)item.GetSnapshot()).Extra.Should().Be(41);
    }
}
