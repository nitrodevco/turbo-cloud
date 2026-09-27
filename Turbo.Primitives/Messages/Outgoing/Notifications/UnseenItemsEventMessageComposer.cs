using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

/// <summary>
/// Items the client should draw as "new", per inventory tab. The client adds these to what it
/// already marks, so the login burst carries everything and a later one only what arrived.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record UnseenItemsEventMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableDictionary<
        UnseenItemCategory,
        ImmutableArray<int>
    > Items { get; init; }
}
