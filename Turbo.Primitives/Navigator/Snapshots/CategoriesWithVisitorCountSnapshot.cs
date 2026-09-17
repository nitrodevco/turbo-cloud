using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Navigator.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CategoriesWithVisitorCountSnapshot
{
    [Id(0)]
    public required Dictionary<int, List<int>> CategoriesWithVisitorCount;
}
