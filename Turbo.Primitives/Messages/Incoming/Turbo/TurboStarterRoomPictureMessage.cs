using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>starter.rooms</c>): the render data of the room under the
/// starter room camera, compressed as a photo's is (<c>RenderRoomMessage</c>), and the room as the
/// camera showed it, a PNG - walls and floors with their textures, which the render data does not
/// carry. Empty from a client that sends only the data.
/// </summary>
public record TurboStarterRoomPictureMessage : IMessageEvent
{
    public required int RoomId { get; init; }

    public required byte[] Data { get; init; }

    public byte[] Scene { get; init; } = [];
}
