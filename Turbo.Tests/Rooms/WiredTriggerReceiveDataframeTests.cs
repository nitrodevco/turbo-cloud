using FluentAssertions;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIRED Trigger: Receive Link Dataframe (<c>wf_trg_receive_dataframe</c>, trigger code 30; AS3 WIN63-202610201032-107964540 <c>roomevents/userdefinedroomevents/triggerconfs/ReceiveLinkDataframe.as</c>).
/// Its code; what it saves, refuses and does is in WiredLinkBoxesTests.
/// </summary>
public sealed class WiredTriggerReceiveDataframeTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void The_box_is_trigger_code_30()
    {
        var item = _room.AddFloorItem(40, 1, 1, "wf_trg_receive_dataframe");

        var instance = _room.Harness.LogicProvider.CreateLogicInstance(
            "wf_trg_receive_dataframe",
            new RoomFloorItemContext(_room.Harness.Room, item)
        );

        instance.Should().BeOfType<WiredTriggerReceiveDataframe>();
        ((FurnitureWiredLogic)instance!).WiredCode.Should().Be(30);
    }
}
