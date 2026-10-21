using FluentAssertions;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIRED Effect: Send Link Message (<c>wf_act_send_message</c>, action code 63; AS3 WIN63-202610201032-107964540 <c>roomevents/userdefinedroomevents/actiontypes/SendLinkMessage.as</c>).
/// Its code; what it saves, refuses and does is in WiredLinkBoxesTests.
/// </summary>
public sealed class WiredActionSendMessageTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void The_box_is_action_code_63()
    {
        var item = _room.AddFloorItem(40, 1, 1, "wf_act_send_message");

        var instance = _room.Harness.LogicProvider.CreateLogicInstance(
            "wf_act_send_message",
            new RoomFloorItemContext(_room.Harness.Room, item)
        );

        instance.Should().BeOfType<WiredActionSendMessage>();
        ((FurnitureWiredLogic)instance!).WiredCode.Should().Be(63);
    }
}
