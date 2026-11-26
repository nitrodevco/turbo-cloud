using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// A user leaving (a player gone, a bot picked up) is out of the room before wired hears of it,
/// and the stacks it fires run later still: at the end of the call or on the next tick. Those
/// stacks are about the leaver, so until their execution has run the leaver keeps the values it
/// held and its variables are read off the avatar it was. The execution over, the values are
/// dropped; a delayed effect of the same stack finds nobody, as for any user who left since it
/// fired. The leaver is read, never acted on: it stands on no tile, and an effect on the users
/// it selected skips it like anyone else who is gone.
/// </summary>
public sealed partial class RoomWiredSystem
{
    // The user whose leaving is running its stacks now.
    private IRoomAvatar? _leaver;

    /// <summary>
    /// An avatar in the room, or the one whose leaving is running its stacks now. What a variable
    /// reads its target through, and the triggering selection; nothing that changes an avatar.
    /// </summary>
    public bool TryGetAvatarOrLeaver(
        RoomObjectId objectId,
        [NotNullWhen(true)] out IRoomAvatar? avatar
    )
    {
        if (AvatarModule.TryGetAvatar(objectId, out avatar))
            return true;

        if (_leaver is not { } leaver || leaver.ObjectId != objectId)
            return false;

        avatar = leaver;

        return true;
    }

    /// <summary>The avatar an event says has left, if it is one of those.</summary>
    private static RoomObjectId? LeaverOf(RoomEvent evt) =>
        evt switch
        {
            PlayerLeftEvent left => left.ObjectId,
            BotRemovedEvent removed => removed.ObjectId,
            _ => null,
        };

    /// <summary>
    /// Lets go of what the avatar held, at once when no stack will hear of its leaving and after
    /// that execution otherwise (<see cref="RunLeaveAsync"/>).
    /// </summary>
    private void OnAvatarLeft(RoomEvent evt, RoomObjectId objectId)
    {
        ForgetUserVisualization(objectId);

        if (!QueueEvent(evt))
            _playerActiveStore.RemoveAvatarStore(objectId);
    }

    private async Task RunLeaveAsync(
        RoomEvent evt,
        RoomObjectId objectId,
        long now,
        CancellationToken ct
    )
    {
        AvatarModule.TryGetDeparted(objectId, out _leaver);

        try
        {
            await RunExecutionAsync(() => ProcessRoomEventAsync(evt, now, ct), now, ct);
        }
        finally
        {
            _leaver = null;
            _playerActiveStore.RemoveAvatarStore(objectId);
        }
    }

    /// <summary>Drops the events waiting to run, and what the leavers among them still held.</summary>
    private void DropQueuedEvents()
    {
        foreach (var evt in _eventQueue)
        {
            if (LeaverOf(evt) is { } objectId)
                _playerActiveStore.RemoveAvatarStore(objectId);
        }

        _eventQueue.Clear();
    }
}
