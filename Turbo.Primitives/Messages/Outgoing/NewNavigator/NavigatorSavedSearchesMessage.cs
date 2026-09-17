using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

[GenerateSerializer, Immutable]
public sealed record NavigatorSavedSearchesMessage : IComposer
{
    [Id(0)]
    public required List<NavigatorQuickLinkSnapshot> SavedSearches { get; init; }
}
