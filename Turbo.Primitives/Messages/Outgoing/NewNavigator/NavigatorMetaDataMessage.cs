using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.NewNavigator;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public record NavigatorMetaDataMessage : IComposer
{
    public required List<NavigatorTopLevelContextsSnapshot> TopLevelContexts { get; init; }
}
