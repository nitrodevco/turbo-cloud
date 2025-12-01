using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record NoobnessLevelMessage : IComposer
{
    public required NoobnessLevelType NoobnessLevel { get; init; }
}
