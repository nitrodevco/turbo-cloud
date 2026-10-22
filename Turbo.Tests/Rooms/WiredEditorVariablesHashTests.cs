using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A box editor that lists the room's variables carries only their hash, and the client asks for
/// the list again only when that hash differs from the one it synchronised last
/// (<c>WiredVariablesSynchronizer.getAllVariables</c> with the triggerable's hash,
/// <c>UserDefinedRoomEventsCtrl.synchronizeTriggerable</c>). A Pointer Variable opened after a
/// new context variable was saved must carry a hash that tells the client its list is old.
/// </summary>
public sealed class WiredEditorVariablesHashTests
{
    private const int GLOBAL = 10;
    private const int CONTEXT = 11;
    private const int POINTER = 12;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task An_editor_opened_after_a_new_variable_carries_the_new_list_hash()
    {
        _room.AddBox<WiredVariableRoom>(GLOBAL, 4, 4, "wf_var_room");
        _room.AddBox<WiredVariablePointer>(POINTER, 5, 5, "wf_var_pointer");

        await SaveVariableAsync(GLOBAL, [(int)WiredAvailabilityType.RoomActive], "copied");
        await SaveVariableAsync(POINTER, [(int)WiredVariableTargetType.Furni], "pointed");

        // The client synchronises the list once, from the pointer's editor.
        var before = await Wired.GetWiredVariablesSnapshotAsync(Ct);

        (await EditorHashAsync()).Should().Be(before.AllVariablesHash);

        var context = _room.AddBox<WiredVariableContext>(CONTEXT, 3, 3, "wf_var_context");

        await SaveVariableAsync(CONTEXT, [1], "carried");

        var editorHash = await EditorHashAsync();
        var after = await Wired.GetWiredVariablesSnapshotAsync(Ct);

        after
            .Variables.Select(x => x.VariableId)
            .Should()
            .Contain(context.GetVarSnapshot().VariableId);
        editorHash.Should().NotBe(before.AllVariablesHash);
        editorHash.Should().Be(after.AllVariablesHash);
    }

    private async Task<WiredVariableHash> EditorHashAsync() =>
        (await Wired.GetBoxSnapshotAsync((RoomObjectId)POINTER, Ct))!
            .ContextSnapshots.OfType<WiredVariableAllInRoomSnapshot>()
            .Single()
            .AllVariablesHash;

    private async Task SaveVariableAsync(int boxId, int[] intParams, string name)
    {
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                boxId,
                intParams: intParams,
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await Wired.OnRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                BoxIds = [boxId],
            },
            Ct
        );
        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
    }
}
