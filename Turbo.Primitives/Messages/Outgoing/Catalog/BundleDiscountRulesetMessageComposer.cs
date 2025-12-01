using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record BundleDiscountRulesetMessageComposer : IComposer
{
    [Id(0)]
    public required BundleDiscountRulesetSnapshot BundleDiscountRuleset { get; init; }
}
