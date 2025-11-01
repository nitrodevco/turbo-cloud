using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.Navigator;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record CategoriesWithVisitorCountMessageComposer : IComposer
{
    public required CategoriesWithVisitorCountSnapshot Categories { get; init; }
}
