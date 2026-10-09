using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Camera;

/// <summary><c>RenderRoomThumbnailMessageComposer</c>: the room thumbnail's render data, as <see cref="RenderRoomMessage"/>.</summary>
public record RenderRoomThumbnailMessage : IMessageEvent
{
    public required byte[] Data { get; init; }
}
