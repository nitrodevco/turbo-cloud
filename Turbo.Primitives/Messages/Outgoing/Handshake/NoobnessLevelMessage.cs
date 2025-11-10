using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Players;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record NoobnessLevelMessage : IComposer
{
    public required NoobnessLevelEnum NoobnessLevel { get; init; }
}
