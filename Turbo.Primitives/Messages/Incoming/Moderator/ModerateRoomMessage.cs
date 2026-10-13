using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>ModerateRoomMessageComposer(flatId, lock, changeName, kick)</c>: the room tool's checkboxes (<c>RoomToolCtrl.as:365</c>), each sent as an int 0 or 1.</summary>
public record ModerateRoomMessage : IMessageEvent
{
    public required int RoomId { get; init; }

    /// <summary>"Change room lock to doorbell".</summary>
    public required bool LockDoor { get; init; }

    /// <summary>"Change room name to 'Inappropriate to hotel management'".</summary>
    public required bool ChangeName { get; init; }

    /// <summary>"Kick users out of the room (automatically stops the event)".</summary>
    public required bool Kick { get; init; }
}
