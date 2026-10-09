using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Camera;

/// <summary>
/// <c>RenderRoomMessageComposer</c>: the photo's render data, zlib-deflated JSON, sent as one byte
/// array (its length, then its bytes).
/// </summary>
public record RenderRoomMessage : IMessageEvent
{
    public required byte[] Data { get; init; }
}
