using System.Collections.Immutable;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public sealed record NavigatorMetaDataMessage : IComposer
{
    public required ImmutableArray<NavigatorTopLevelContextSnapshot> TopLevelContexts { get; init; }
}
