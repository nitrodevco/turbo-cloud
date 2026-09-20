using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record ClubGiftInfoEventMessageComposer : IComposer
{
    [Id(0)]
    public required ClubGiftInfoSnapshot Info { get; init; }
}
