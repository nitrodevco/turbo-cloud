using FluentAssertions;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIRED Link Speaker (<c>wf_xtra_link_speaker</c>, addon code 2003; AS3 WIN63-202610201032-107964540 <c>roomevents/wired_setup/addons/_-z14.as</c>).
/// Its code; what it saves, refuses and does is in WiredLinkBoxesTests.
/// </summary>
public sealed class WiredAddonLinkSpeakerTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void The_box_is_addon_code_2003()
    {
        var item = _room.AddFloorItem(40, 1, 1, "wf_xtra_link_speaker");

        var instance = _room.Harness.LogicProvider.CreateLogicInstance(
            "wf_xtra_link_speaker",
            new RoomFloorItemContext(_room.Harness.Room, item)
        );

        instance.Should().BeOfType<WiredAddonLinkSpeaker>();
        ((FurnitureWiredLogic)instance!).WiredCode.Should().Be(2003);
    }
}
