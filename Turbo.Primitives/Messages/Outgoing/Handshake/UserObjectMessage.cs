using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record UserObjectMessage : IComposer
{
    public required PlayerSummarySnapshot Player { get; init; }
}
