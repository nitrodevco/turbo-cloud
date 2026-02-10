using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Players;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record UserObjectMessage : IComposer
{
    public required PlayerSummarySnapshot Player { get; init; }
    public required int MaxRespectPerDay { get; init; }
}
