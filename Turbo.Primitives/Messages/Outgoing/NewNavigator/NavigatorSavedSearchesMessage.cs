using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Navigator;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public sealed record NavigatorSavedSearchesMessage : IComposer
{
    public required List<NavigatorQuickLinkSnapshot> SavedSearches { get; init; }
}
