using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>Tells the user an offer in their trade with wired was not taken, and why.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTradeTransactionNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required WiredTradeErrorType Error { get; init; }
}
