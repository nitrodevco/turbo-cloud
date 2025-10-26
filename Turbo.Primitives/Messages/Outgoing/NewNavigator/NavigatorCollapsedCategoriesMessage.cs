using System.Collections.Generic;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public record NavigatorCollapsedCategoriesMessage : IComposer
{
    public required List<string> CollapsedCategoryIds { get; init; }
}
