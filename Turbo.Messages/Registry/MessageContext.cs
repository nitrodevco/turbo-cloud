using Turbo.Primitives.Networking;

namespace Turbo.Messages.Registry;

public sealed class MessageContext
{
    public required long PlayerId { get; init; }
    public required long RoomId { get; init; }
    public required ISessionContext Session { get; init; }
}
