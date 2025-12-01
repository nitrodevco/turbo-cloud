using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Vault;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

[GenerateSerializer, Immutable]
public sealed record IncomeRewardStatusMessageComposer : IComposer
{
    [Id(0)]
    public required List<IncomeRewardSnapshot> IncomeRewards { get; init; }
}
