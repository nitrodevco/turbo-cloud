using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Picking up a wired variable box deletes what it stored and orphans every box that names it,
/// so the player is asked first (<c>ObjectRemoveConfirm</c>, which the server never sent: its
/// composer had no payload and the pickup ignored the client's confirm flag).
/// </summary>
public sealed class FurniturePickupConfirmTests
{
    private const int OWNER = 1;
    private const int VARIABLE = 40;
    private const int CHAIR = 41;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    [Fact]
    public async Task A_variable_box_is_picked_up_only_once_the_player_confirms()
    {
        _room.AddBox<WiredVariableUser>(VARIABLE, 3, 3, "wf_var_user");

        (await _room.Harness.Room.RemoveItemByIdAsync(Owner, VARIABLE, false, Ct))
            .Should()
            .BeFalse();

        ConfirmsSentToOwner()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new ObjectRemoveConfirmMessageComposer
                {
                    Category = RoomItemCategoryType.Floor,
                    ObjectId = VARIABLE,
                    Confirmation = FurniturePickupConfirmation.WiredVariable,
                }
            );
        ItemsById.Contains((RoomObjectId)VARIABLE).Should().BeTrue();

        (await _room.Harness.Room.RemoveItemByIdAsync(Owner, VARIABLE, true, Ct)).Should().BeTrue();

        ItemsById.Contains((RoomObjectId)VARIABLE).Should().BeFalse();
        ConfirmsSentToOwner().Should().ContainSingle("the confirmed pickup asks nothing more");
    }

    [Fact]
    public async Task Ordinary_furni_is_picked_up_without_asking()
    {
        _room.AddFloorItem(CHAIR, 3, 3);

        (await _room.Harness.Room.RemoveItemByIdAsync(Owner, CHAIR, false, Ct)).Should().BeTrue();

        ConfirmsSentToOwner().Should().BeEmpty();
        ItemsById.Contains((RoomObjectId)CHAIR).Should().BeFalse();
    }

    [Fact]
    public void The_confirm_goes_out_as_the_client_reads_it()
    {
        var packet = PacketHarness.Encode(
            new ObjectRemoveConfirmMessageComposer
            {
                Category = RoomItemCategoryType.Floor,
                ObjectId = VARIABLE,
                Confirmation = FurniturePickupConfirmation.WiredVariable,
            }
        );

        packet.PopInt().Should().Be(2, "the client reads anything but 1 as a floor item");
        packet.PopInt().Should().Be(VARIABLE);
        packet.PopString().Should().Be("wiredfurni.variables.pickup.title");
        packet.PopString().Should().Be("wiredfurni.variables.pickup.body");
        packet.Remaining.Should().Be(0);
    }

    private IDictionary ItemsById => _room.Harness.State.ItemsById;

    private IEnumerable<ObjectRemoveConfirmMessageComposer> ConfirmsSentToOwner() =>
        _room.Harness.Fakes.SentToPlayer<ObjectRemoveConfirmMessageComposer>(OWNER);
}
