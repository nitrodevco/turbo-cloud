using Turbo.Primitives.Networking;

namespace Turbo.Messages.Registry;

public sealed class MessageContext
{
    public required ISessionContext Session { get; init; }
}
