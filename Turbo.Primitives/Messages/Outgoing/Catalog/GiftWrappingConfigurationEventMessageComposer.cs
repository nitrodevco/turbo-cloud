using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record GiftWrappingConfigurationEventMessageComposer : IComposer
{
    [Id(0)]
    public required GiftWrappingSnapshot Wrapping { get; init; }
}
