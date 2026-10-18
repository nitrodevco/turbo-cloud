using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Rooms.Object;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// The inventory's two furni list messages that were written empty or not at all, as the client
/// reads them (AS3 WIN63-202610151745): FurniListRemoveMultiple is a count, then each strip id
/// (<c>inventory/furni/§_-7u§.parse</c>); PostItPlaced is the stack's strip id, then the sheets
/// left (<c>PostItPlacedParser.parse</c>). A client reading either from an empty packet read past
/// the end.
/// </summary>
public sealed class FurniListRemoveMultipleWireTests
{
    [Fact]
    public void Remove_multiple_writes_the_count_then_each_strip_id()
    {
        var packet = PacketHarness.Encode(
            new FurniListRemoveMultipleEventMessageComposer
            {
                ItemIds = [new RoomObjectId(-12), new RoomObjectId(40), new RoomObjectId(41)],
            }
        );

        PacketHarness
            .Revision.Serializers[typeof(FurniListRemoveMultipleEventMessageComposer)]
            .Header.Should()
            .Be(PacketHarness.Outgoing("FurniListRemoveMultipleMessageComposer"));
        packet.PopInt().Should().Be(3);
        packet.PopInt().Should().Be(-12);
        packet.PopInt().Should().Be(40);
        packet.PopInt().Should().Be(41);
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void Post_it_placed_writes_the_stack_then_the_sheets_left()
    {
        var packet = PacketHarness.Encode(
            new PostItPlacedEventMessageComposer { ItemId = new RoomObjectId(77), ItemsLeft = 19 }
        );

        PacketHarness
            .Revision.Serializers[typeof(PostItPlacedEventMessageComposer)]
            .Header.Should()
            .Be(PacketHarness.Outgoing("PostItPlacedMessageComposer"));
        packet.PopInt().Should().Be(77);
        packet.PopInt().Should().Be(19);
        packet.Remaining.Should().Be(0);
    }
}
