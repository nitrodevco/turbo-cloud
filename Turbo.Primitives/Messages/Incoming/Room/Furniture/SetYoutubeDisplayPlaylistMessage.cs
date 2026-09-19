using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>Switches a video display to another playlist.</summary>
public record SetYoutubeDisplayPlaylistMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required string PlaylistId { get; init; }
}
