using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A box's editor can be opened and saved before the wired tick has loaded the box (it was just
/// placed, or the room has only just woken).
/// </summary>
public class WiredBoxEditorTests
{
    private const int BoxId = 7;

    [Fact]
    public async Task Editor_opens_a_box_the_wired_tick_has_not_loaded_yet()
    {
        var room = CreateRoomWithBox();

        var snapshot = await room.Room.GetWiredDataSnapshotByFloorItemIdAsync(
            ActionContext.CreateForSystem(1),
            BoxId,
            CancellationToken.None
        );

        Assert.NotNull(snapshot);
        Assert.Equal(BoxId, snapshot.Id);
        Assert.Equal((int)WiredTriggerType.TRIGGER_PERIODICALLY, snapshot.Code);
        Assert.Equal([1], snapshot.IntParams);
    }

    [Fact]
    public async Task Editor_saves_a_box_the_wired_tick_has_not_loaded_yet()
    {
        var room = CreateRoomWithBox();

        var saved = await room.Room.ApplyWiredUpdateAsync(
            ActionContext.CreateForSystem(1),
            BoxId,
            new UpdateWiredMessage
            {
                Id = BoxId,
                IntParams = [5],
                StringParam = "",
                StuffIds = [],
                StuffIds2 = [],
                DefinitionSpecifics = [],
                FurniSources = [],
                PlayerSources = [],
                VariableIds = [],
                TypeSpecifics = [],
            },
            CancellationToken.None
        );

        Assert.True(saved);
    }

    private static RoomHarness CreateRoomWithBox()
    {
        var room = new RoomHarness();
        RoomHarness.SetMember(room.State, "IsRightsLoaded", true);
        room.AddToRoom(
            room.CreateFloorItem(
                BoxId,
                2,
                2,
                Altitude.Zero,
                name: "wf_trg_periodically",
                logic: "wf_trg_periodically",
                createLogic: (stuffDataFactory, ctx) =>
                    new WiredTriggerPeriodically(
                        room.Fakes.Create<IGrainFactory>(),
                        stuffDataFactory,
                        ctx
                    )
            )
        );
        return room;
    }
}
