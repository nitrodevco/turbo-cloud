using FluentAssertions;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIRED Trigger: Receive Link Message (<c>wf_trg_receive_message</c>, trigger code 29; AS3 WIN63-202610201032-107964540 <c>roomevents/userdefinedroomevents/triggerconfs/ReceiveLinkMessage.as</c>).
/// Its code; what it saves, refuses and does is in WiredLinkBoxesTests.
/// </summary>
public sealed class WiredTriggerReceiveMessageTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void The_box_is_trigger_code_29()
    {
        var item = _room.AddFloorItem(40, 1, 1, "wf_trg_receive_message");

        var instance = _room.Harness.LogicProvider.CreateLogicInstance(
            "wf_trg_receive_message",
            new RoomFloorItemContext(_room.Harness.Room, item)
        );

        instance.Should().BeOfType<WiredTriggerReceiveMessage>();
        ((FurnitureWiredLogic)instance!).WiredCode.Should().Be(29);
    }
}
