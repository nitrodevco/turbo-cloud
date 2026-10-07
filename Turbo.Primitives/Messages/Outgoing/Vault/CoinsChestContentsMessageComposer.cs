using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

/// <summary>The coins held in a wired chest.</summary>
[GenerateSerializer, Immutable]
public sealed record CoinsChestContentsMessageComposer : IComposer
{
    [Id(0)]
    public required int ChestId { get; init; }

    [Id(1)]
    public required int Coins { get; init; }

    /// <summary>False for the first answer to an open, true for a later change.</summary>
    [Id(2)]
    public required bool IsUpdate { get; init; }
}
