using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>Opens a wired trade with the user: what is asked of them and how long they have.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTradeInitiateMessageComposer : IComposer
{
    [Id(0)]
    public required TradeRequirementSnapshot Requirement { get; init; }

    [Id(1)]
    public required bool ShowRequirementsImmediate { get; init; }

    [Id(2)]
    public required bool OverridePreviousTrade { get; init; }

    [Id(3)]
    public required int TimeoutSeconds { get; init; }
}
