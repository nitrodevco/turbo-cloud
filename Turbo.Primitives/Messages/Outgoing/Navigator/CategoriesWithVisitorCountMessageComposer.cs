using Orleans;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record CategoriesWithVisitorCountMessageComposer : IComposer
{
    [Id(0)]
    public required CategoriesWithVisitorCountSnapshot Categories { get; init; }
}
