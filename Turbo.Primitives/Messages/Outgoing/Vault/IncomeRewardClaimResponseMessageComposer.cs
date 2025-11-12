using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

[GenerateSerializer, Immutable]
public sealed record IncomeRewardClaimResponseMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
