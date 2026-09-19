using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>Asks a video display for its playlists and what it is playing.</summary>
public record GetYoutubeDisplayStatusMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
