using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>
/// A click on an avatar, sent by the client only while the room says it has a "user clicks user"
/// trigger (<c>WiredEnvironment</c>); the client then waits for <c>WiredClickUserResponse</c>
/// before it opens the avatar menu, and does not turn its avatar itself.
/// </summary>
public record WiredClickUserMessage : IMessageEvent
{
    /// <summary>The room index of the clicked avatar.</summary>
    public required RoomObjectId ObjectId { get; init; }
}
