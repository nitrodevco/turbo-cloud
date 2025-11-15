using System.Collections.Immutable;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Navigator;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public sealed record NavigatorMetaDataMessage : IComposer
{
    public required ImmutableArray<NavigatorTopLevelContextSnapshot> TopLevelContexts { get; init; }
}
