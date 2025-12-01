using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public sealed record NavigatorCollapsedCategoriesMessage : IComposer
{
    public required List<string> CollapsedCategoryIds { get; init; }
}
