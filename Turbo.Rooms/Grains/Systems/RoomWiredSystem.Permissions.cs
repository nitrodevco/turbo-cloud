using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Who may read and modify the room's wired. The room stores two masks; what a controller level
/// makes of them is decided here, and the client is told from here, whenever the security
/// module says a player's level changed or the masks themselves are saved.
/// </summary>
public sealed partial class RoomWiredSystem
{
    /// <summary>The owner and staff always pass; group admins count as group members.</summary>
    public (bool canModify, bool canRead) GetPermissions(RoomControllerType controllerLevel)
    {
        var snapshot = _roomGrain._state.RoomSnapshot;

        return (
            IsPermitted(snapshot.WiredModifyPermissionMask, controllerLevel),
            IsPermitted(snapshot.WiredReadPermissionMask, controllerLevel)
        );
    }

    /// <summary>Tells everyone in the room again, for when the masks change.</summary>
    public async Task RefreshPermissionsForRoomAsync(CancellationToken ct)
    {
        var updates = new List<Task>();

        foreach (var playerId in _roomGrain._state.AvatarsByPlayerId.Keys.ToList())
            updates.Add(
                SendPermissionsAsync(
                    playerId,
                    await _roomGrain.SecurityModule.GetControllerLevelAsync(playerId),
                    ct
                )
            );

        // One presence grain per player, so the updates do not wait on each other.
        await Task.WhenAll(updates);
    }

    private Task SendPermissionsAsync(
        PlayerId playerId,
        RoomControllerType controllerLevel,
        CancellationToken ct
    )
    {
        // A level also changes for players who are elsewhere (rights given to someone not in
        // the room); only the ones standing here are shown this room's permissions.
        if (!_roomGrain._state.AvatarsByPlayerId.ContainsKey(playerId))
            return Task.CompletedTask;

        var (canModify, canRead) = GetPermissions(controllerLevel);

        return _roomGrain._grainFactory.SendComposerToPlayerAsync(
            playerId,
            new WiredPermissionsEventMessageComposer { CanModify = canModify, CanRead = canRead },
            ct
        );
    }

    private static bool IsPermitted(WiredPermissionFlags mask, RoomControllerType level) =>
        level switch
        {
            >= RoomControllerType.Owner => true,
            RoomControllerType.GroupAdmin => mask.HasFlag(WiredPermissionFlags.GroupAdmins)
                || mask.HasFlag(WiredPermissionFlags.GroupMembers)
                || mask.HasFlag(WiredPermissionFlags.Everyone),
            RoomControllerType.GroupRights => mask.HasFlag(WiredPermissionFlags.GroupMembers)
                || mask.HasFlag(WiredPermissionFlags.Everyone),
            RoomControllerType.Rights => mask.HasFlag(WiredPermissionFlags.Rights)
                || mask.HasFlag(WiredPermissionFlags.Everyone),
            _ => mask.HasFlag(WiredPermissionFlags.Everyone),
        };
}
