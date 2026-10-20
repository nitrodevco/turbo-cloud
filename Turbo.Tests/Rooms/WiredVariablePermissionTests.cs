using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Permanent, shared" is the room owner's to set up: anyone else saving it gets
/// <c>wiredfurni.error.variables.shared_var_own_room</c> ("You can only set up shared variables
/// in your own room."). An availability the box's editor does not offer (furni: 1, 10; global:
/// 1, 10, 11; user: 0, 10, 11, AS3 WIN63-202610201032 wired_setup/variables/FurniVariable.as:62,
/// GlobalVariable.as:60, UserVariable.as:65) gets <c>invalid_availability</c>. Ours let a player
/// with rights share a variable of another owner's room and answered a bad availability with the
/// generic error.
/// </summary>
public sealed class WiredVariablePermissionTests
{
    private const int OWNER = 1;
    private const int GUEST = 2;
    private const int ROOM_VAR = 2;
    private const int USER_VAR = 3;
    private const int FURNI_VAR = 4;

    private readonly WiredRoom _room = new(8, 8);

    public WiredVariablePermissionTests()
    {
        ((ISet<PlayerId>)RoomHarness.GetMember(_room.Harness.State, "PlayerIdsWithRights")!).Add(
            GUEST
        );
        RoomHarness.SetMember(
            RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!,
            "WiredModifyPermissionMask",
            WiredPermissionFlags.Rights
        );
    }

    [Fact]
    public async Task Only_the_room_owner_may_set_up_a_shared_variable()
    {
        _room.AddBox<WiredVariableRoom>(ROOM_VAR, 0, 0, "wf_var_room");
        _room.AddBox<WiredVariableUser>(USER_VAR, 1, 0, "wf_var_user");

        (await SaveAsync(ROOM_VAR, GUEST, "points", [(int)WiredAvailabilityType.Shared]))
            .ErrorKey.Should()
            .Be(WiredSaveErrors.VARIABLE_SHARED_OWN_ROOM);
        (await SaveAsync(USER_VAR, GUEST, "level", [(int)WiredAvailabilityType.Shared, 0]))
            .ErrorKey.Should()
            .Be(WiredSaveErrors.VARIABLE_SHARED_OWN_ROOM);

        (await SaveAsync(ROOM_VAR, GUEST, "points", [(int)WiredAvailabilityType.Persistent]))
            .IsSaved.Should()
            .BeTrue("only sharing is the owner's");
        (await SaveAsync(USER_VAR, OWNER, "level", [(int)WiredAvailabilityType.Shared, 0]))
            .IsSaved.Should()
            .BeTrue();
    }

    [Fact]
    public async Task An_availability_the_editor_does_not_offer_is_refused_as_invalid()
    {
        _room.AddBox<WiredVariableFurni>(FURNI_VAR, 0, 0, "wf_var_furni");
        _room.AddBox<WiredVariableUser>(USER_VAR, 1, 0, "wf_var_user");

        (await SaveAsync(FURNI_VAR, OWNER, "points", [0, (int)WiredAvailabilityType.Shared]))
            .ErrorKey.Should()
            .Be(WiredSaveErrors.VARIABLE_INVALID_AVAILABILITY);
        (await SaveAsync(USER_VAR, OWNER, "level", [(int)WiredAvailabilityType.RoomActive, 0]))
            .ErrorKey.Should()
            .Be(WiredSaveErrors.VARIABLE_INVALID_AVAILABILITY);
    }

    private Task<WiredSaveResult> SaveAsync(int boxId, int playerId, string name, int[] intParams) =>
        _room.SaveWithResultAsync<UpdateVariableMessage>(
            boxId,
            intParams: intParams,
            stringParam: name,
            actor: ActionContext.CreateForPlayer(playerId, 1)
        );
}
