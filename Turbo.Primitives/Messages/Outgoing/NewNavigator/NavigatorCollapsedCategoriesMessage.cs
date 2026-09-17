using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

[GenerateSerializer, Immutable]
public sealed record NavigatorCollapsedCategoriesMessage : IComposer
{
    [Id(0)]
    public required List<string> CollapsedCategoryIds { get; init; }
}
