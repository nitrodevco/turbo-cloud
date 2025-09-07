using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Dtos.Players;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record UserObjectMessage : IComposer
{
    public PlayerSummary Player { get; init; }
}
