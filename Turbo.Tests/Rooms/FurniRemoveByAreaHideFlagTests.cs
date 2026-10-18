using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIN63-202610121624 reads one more boolean at the end of ObjectRemove, ItemRemove,
/// ObjectRemoveMultiple and ItemRemoveMultiple (<c>byInvisibleFurniManager</c>, read without a
/// bytes-available check). True keeps what the client buffers for the furni (labels, regions,
/// wired highlight and transparency, variable fx) to restore when it is sent again, which is
/// what an area hider does: it takes furni away and sends them back. A pickup says false.
/// </summary>
public sealed class FurniRemoveByAreaHideFlagTests
{
    private const int HIDER = 40;
    private const int CRATE = 44;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_remove_message_ends_with_the_flag()
    {
        var floor = PacketHarness.Encode(
            new ObjectRemoveMessageComposer
            {
                ObjectId = 7,
                IsExpired = false,
                PickerId = (PlayerId)3,
                Delay = 0,
                ByInvisibleFurniManager = true,
            }
        );
        floor.PopString().Should().Be("7");
        floor.PopBoolean().Should().BeFalse();
        floor.PopInt().Should().Be(3);
        floor.PopInt().Should().Be(0);
        floor.PopBoolean().Should().BeTrue();
        floor.Remaining.Should().Be(0);

        var wall = PacketHarness.Encode(
            new ItemRemoveMessageComposer
            {
                ObjectId = 8,
                PickerId = (PlayerId)3,
                ByInvisibleFurniManager = true,
            }
        );
        wall.PopString().Should().Be("8");
        wall.PopInt().Should().Be(3);
        wall.PopBoolean().Should().BeTrue();
        wall.Remaining.Should().Be(0);

        var several = PacketHarness.Encode(
            new ObjectRemoveMultipleMessageComposer
            {
                ObjectIdsToRemove = [7, 9],
                PickerId = (PlayerId)3,
                ByInvisibleFurniManager = false,
            }
        );
        several.PopInt().Should().Be(2);
        several.PopInt().Should().Be(7);
        several.PopInt().Should().Be(9);
        several.PopInt().Should().Be(3);
        several.PopBoolean().Should().BeFalse();
        several.Remaining.Should().Be(0);

        // ItemRemoveMultiple (WIN63-202610151745 RoomMessageHandler.as:286/911): int ids, unlike ItemRemove.
        var walls = PacketHarness.Encode(
            new ItemRemoveMultipleMessageComposer
            {
                ObjectIdsToRemove = [8, 10],
                PickerId = (PlayerId)3,
                ByInvisibleFurniManager = true,
            }
        );
        walls.PopInt().Should().Be(2);
        walls.PopInt().Should().Be(8);
        walls.PopInt().Should().Be(10);
        walls.PopInt().Should().Be(3);
        walls.PopBoolean().Should().BeTrue();
        walls.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task An_area_hider_takes_furni_away_with_the_flag_and_a_pickup_without()
    {
        var room = new WiredRoom(8, 8);
        var hider = room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        var crate = room.AddFloorItem(CRATE, 2, 3);
        var owner = ActionContext.CreateForSystem(1);

        await hider.Logic.OnInteractAsync(
            owner,
            new SetAreaHideInteraction
            {
                RootX = 2,
                RootY = 2,
                Width = 2,
                Length = 3,
                Invisibility = false,
                WallItems = false,
                Invert = false,
            },
            Ct
        );
        await hider.Logic.SetStateAsync(1, false);

        Removes(room)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<ObjectRemoveMessageComposer>(x =>
                x.ObjectId == (RoomObjectId)CRATE && x.ByInvisibleFurniManager
            );

        await hider.Logic.SetStateAsync(0, false);
        await room.Harness.Module<RoomObjectModule>().RemoveObjectAsync(owner, crate, Ct, 1);

        Removes(room).Last().ByInvisibleFurniManager.Should().BeFalse();
    }

    private static List<ObjectRemoveMessageComposer> Removes(WiredRoom room) =>
        room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<ObjectRemoveMessageComposer>()
            .ToList();
}
