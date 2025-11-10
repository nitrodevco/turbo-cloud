using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

public sealed record BundleDiscountRulesetMessageComposer : IComposer
{
    public required BundleDiscountRulesetSnapshot BundleDiscountRuleset { get; init; }
}
