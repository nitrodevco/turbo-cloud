using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Players;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record UserObjectMessage : IComposer
{
    public required PlayerSnapshot Player { get; init; }
}
