using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Rooms;

/// <summary>
/// What a staff member may do to a room from the panel. The panel asks for the nodes the hotel
/// asks for, so a room is edited and moderated from the browser exactly as it would be by the same
/// person standing in it: its owner, or <c>room.control.any</c>, may change its settings and
/// rights; <c>room.moderate.any</c> kicks, mutes and bans (the room still decides who outranks
/// whom); and each room-wide action needs its command's node.
/// </summary>
public static class AdminRoomAbilities
{
    public static async Task<RoomAbilities> ForAsync(
        IGrainFactory grainFactory,
        PlayerId viewer,
        int ownerId,
        CancellationToken ct
    )
    {
        var resolved = await grainFactory
            .GetPlayerPermissionGrain(viewer)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);
        var owns = viewer.Value == ownerId;
        var controls = owns || resolved.Has(PermissionNodes.Room.CONTROL_ANY);

        return new RoomAbilities(
            EditSettings: controls,
            StaffPick: resolved.Has(PermissionNodes.Navigator.STAFF_PICK),
            Moderate: owns || resolved.Has(PermissionNodes.Room.MODERATE_ANY),
            ManageRights: controls,
            KickAll: resolved.Has(PermissionNodes.Command.ROOMKICKALL),
            MuteRoom: resolved.Has(PermissionNodes.Command.ROOMMUTE)
                || resolved.Has(PermissionNodes.Command.ROOMUNMUTE),
            Unload: resolved.Has(PermissionNodes.Command.UNLOADROOM),
            Alert: resolved.Has(PermissionNodes.Command.ROOMALERT)
        );
    }
}
