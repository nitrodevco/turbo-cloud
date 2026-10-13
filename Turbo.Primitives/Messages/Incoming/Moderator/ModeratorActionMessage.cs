using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>ModeratorActionMessageComposer(actionType, message, extra)</c>: the room tool's caution or message to everyone in the room.</summary>
public record ModeratorActionMessage : IMessageEvent
{
    /// <summary>0 caution, 1 caution and kick, 3 message, 4 message and kick (<c>RoomToolCtrl.as:313-320</c>).</summary>
    public required int ActionType { get; init; }

    public required string Message { get; init; }

    public required string Extra { get; init; }
}
