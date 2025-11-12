using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.Navigator;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record CategoriesWithVisitorCountMessageComposer : IComposer
{
    [Id(0)]
    public required CategoriesWithVisitorCountSnapshot Categories { get; init; }
}
