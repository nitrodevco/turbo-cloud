using Turbo.Core.Contracts.Players;
using Turbo.Packets.Abstractions;

namespace Turbo.Packets.Outgoing.Handshake;

public record UserObjectMessage : IComposer
{
    public PlayerSummary Player { get; init; }
}
