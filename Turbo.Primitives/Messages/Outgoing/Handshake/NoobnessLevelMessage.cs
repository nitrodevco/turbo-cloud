using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record NoobnessLevelMessage : IComposer
{
    [Id(0)]
    public required NoobnessLevelType NoobnessLevel { get; init; }
}
