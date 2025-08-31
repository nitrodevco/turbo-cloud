using Turbo.Core.Contracts.Players;
using Turbo.Core.Game.Players;
using Turbo.Core.Packets.Messages;

namespace Turbo.Packets.Outgoing.Handshake;

public record UserObjectMessage : IComposer
{
    public PlayerSummary Player { get; init; }
}
