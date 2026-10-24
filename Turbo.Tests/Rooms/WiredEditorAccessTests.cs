using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A wired box's editor opens for the players the room's wired permissions let modify wired, and
/// for nobody else. Habbo's rooms modify and read with the owner and the group's admins until the
/// owner ticks more (WiredRoomSettings 8 / 8 in 5 of 5 recordings), and a rights holder there who
/// double-clicked a box, or pressed Use, got no editor while the owner in the room saw no state
/// change (official client capture 2026-10-24, two accounts, wlabduo-full-20261024-155825, W3 lab
/// trigger.24.E7, 2/2). Ours opened it for every rights holder and then refused their save.
/// </summary>
public sealed class WiredEditorAccessTests
{
    private const int OWNER = 1;
    private const int RIGHTS_HOLDER = 2;
    private const int BOX = 10;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredEditorAccessTests()
    {
        _room.Harness.State.PlayerIdsWithRights.Add(RIGHTS_HOLDER);
        _room.Harness.State.RoomSnapshot = _room.Harness.State.RoomSnapshot with
        {
            WiredModifyPermissionMask = WiredPermissionFlags.GroupAdmins,
            WiredReadPermissionMask = WiredPermissionFlags.GroupAdmins,
        };
    }

    [Fact]
    public async Task Trigger24E7_a_rights_holder_without_wired_permission_gets_no_editor()
    {
        var box = _room.AddBox<WiredSelectorEntitiesByType>(BOX, 3, 3, "wf_slc_users_bytype");
        var state = box.GetState();

        await box.OnUseAsync(ActionContext.CreateForPlayer(RIGHTS_HOLDER, 1), 0, Ct);

        Opened(RIGHTS_HOLDER).Should().BeEmpty();
        box.GetState().Should().Be(state, "the room sees nothing either");

        await box.OnUseAsync(ActionContext.CreateForPlayer(OWNER, 1), 0, Ct);

        Opened(OWNER).Should().ContainSingle().Which.ItemId.Value.Should().Be(BOX);
    }

    [Fact]
    public async Task A_rights_holder_the_owner_lets_modify_wired_gets_the_editor()
    {
        _room.Harness.State.RoomSnapshot = _room.Harness.State.RoomSnapshot with
        {
            WiredModifyPermissionMask = WiredPermissionFlags.Rights,
        };
        var box = _room.AddBox<WiredSelectorEntitiesByType>(BOX, 3, 3, "wf_slc_users_bytype");

        await box.OnUseAsync(ActionContext.CreateForPlayer(RIGHTS_HOLDER, 1), 0, Ct);

        Opened(RIGHTS_HOLDER).Should().ContainSingle();
    }

    private List<OpenEventMessageComposer> Opened(int playerId) =>
        [.. _room.Harness.Fakes.SentToPlayer<OpenEventMessageComposer>(playerId)];
}
