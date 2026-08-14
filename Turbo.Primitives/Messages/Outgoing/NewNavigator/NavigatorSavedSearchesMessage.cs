using System.Collections.Generic;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public sealed record NavigatorSavedSearchesMessage : IComposer
{
    public required List<NavigatorQuickLinkSnapshot> SavedSearches { get; init; }
}
