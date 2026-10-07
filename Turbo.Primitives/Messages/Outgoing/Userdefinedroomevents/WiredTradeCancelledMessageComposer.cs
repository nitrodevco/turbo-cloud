using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>Ends the user's wired trade, saying why.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTradeCancelledMessageComposer : IComposer
{
    [Id(0)]
    public required WiredTransactionFailureType FailureType { get; init; }
}
