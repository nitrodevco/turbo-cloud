using Turbo.Networking.Abstractions.Session;

namespace Turbo.Messages.Registry;

public sealed class MessageContext
{
    public required ISessionContext Session { get; init; }
}
