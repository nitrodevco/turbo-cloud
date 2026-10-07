using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

/// <summary>The outcome of a wired chest upgrade.</summary>
[GenerateSerializer, Immutable]
public sealed record UpgradeChestResultMessageComposer : IComposer
{
    [Id(0)]
    public required int ChestId { get; init; }

    [Id(1)]
    public required UpgradeChestResultType Result { get; init; }
}
