using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>Pause, play, next or previous on a video display.</summary>
public record ControlYoutubeDisplayPlaybackMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required int CommandId { get; init; }
}
