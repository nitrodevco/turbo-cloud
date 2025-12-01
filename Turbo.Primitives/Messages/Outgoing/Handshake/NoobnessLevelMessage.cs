using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record NoobnessLevelMessage : IComposer
{
    public required NoobnessLevelType NoobnessLevel { get; init; }
}
