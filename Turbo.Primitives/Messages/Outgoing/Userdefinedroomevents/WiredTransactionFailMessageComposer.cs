using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>A wired transaction failed, saying why.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionFailMessageComposer : IComposer
{
    [Id(0)]
    public required WiredTransactionFailureType FailureType { get; init; }
}
