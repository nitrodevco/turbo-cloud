using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.NewNavigator;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public record NavigatorSavedSearchesMessage : IComposer
{
    public required List<NavigatorQuickLinkSnapshot> SavedSearches { get; init; }
}
