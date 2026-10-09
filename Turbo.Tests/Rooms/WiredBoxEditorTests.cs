using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A box's editor can be opened and saved before the wired tick has loaded the box (it was just
/// placed, or the room has only just woken), and shows what was last saved when it is opened
/// again straight away.
/// </summary>
public class WiredBoxEditorTests
{
    private const int BoxId = 7;
    private const int PickedId = 8;

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

        Assert.True(saved.IsSaved);
    }

    [Fact]
    public async Task Editor_reopened_after_a_save_shows_the_saved_params()
    {
        var room = CreateRoomWithBox();

        // The first open is what a player sees before editing.
        Assert.Equal([1], (await OpenAsync(room)).IntParams);

        Assert.True(await SaveAsync(room, intParams: [5], stuffIds: []));

        Assert.Equal([5], (await OpenAsync(room)).IntParams);
    }

    [Fact]
    public async Task Editor_reopened_after_a_save_shows_the_picked_furni()
    {
        var room = CreateRoomWithBox();
        room.AddToRoom(room.CreateFloorItem(PickedId, 4, 4, Altitude.Zero));

        Assert.Empty((await OpenAsync(room)).StuffIds);

        Assert.True(await SaveAsync(room, intParams: [1], stuffIds: [PickedId]));

        Assert.Equal([PickedId], (await OpenAsync(room)).StuffIds);
    }

    [Fact]
    public async Task Editor_opens_a_brand_new_box_without_warning_about_its_params()
    {
        var room = CreateRoomWithBox();
        var logger = new CapturingLogger<IRoomGrain>();
        RoomHarness.SetField(room.Room, "_logger", logger);

        // A box that was never saved has no params yet; it takes its defaults quietly.
        Assert.Equal([1], (await OpenAsync(room)).IntParams);
        Assert.Empty(logger.AtLeast(LogLevel.Warning));
    }

    private static async Task<WiredDataSnapshot> OpenAsync(RoomHarness room)
    {
        var snapshot = await room.Room.GetWiredDataSnapshotByFloorItemIdAsync(
            ActionContext.CreateForSystem(1),
            BoxId,
            CancellationToken.None
        );

        Assert.NotNull(snapshot);

        return snapshot;
    }

    private static async Task<bool> SaveAsync(
        RoomHarness room,
        List<int> intParams,
        List<int> stuffIds
    ) =>
        (
            await room.Room.ApplyWiredUpdateAsync(
                ActionContext.CreateForSystem(1),
                BoxId,
                new UpdateWiredMessage
                {
                    Id = BoxId,
                    IntParams = intParams,
                    StringParam = "",
                    StuffIds = stuffIds,
                    StuffIds2 = [],
                    DefinitionSpecifics = [],
                    FurniSources = [],
                    PlayerSources = [],
                    VariableIds = [],
                    TypeSpecifics = [],
                },
                CancellationToken.None
            )
        ).IsSaved;

    private static RoomHarness CreateRoomWithBox()
    {
        var room = new RoomHarness();
        RoomHarness.SetMember(room.State, "IsRightsLoaded", true);

        // The wired permission check reads the room's masks; the owner passes whatever they are.
        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));
        RoomHarness.SetMember(info, "RoomId", (RoomId)1);
        RoomHarness.SetMember(info, "OwnerId", (PlayerId)1);
        RoomHarness.SetMember(room.State, "RoomSnapshot", info);

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
