using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Dtos.Players;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record UserObjectMessage : IComposer
{
    public required PlayerSummary Player { get; init; }
}
