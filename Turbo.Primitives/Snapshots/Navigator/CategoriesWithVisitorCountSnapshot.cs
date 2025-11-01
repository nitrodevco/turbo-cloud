using System.Collections.Generic;

namespace Turbo.Primitives.Snapshots.Navigator;

public sealed record CategoriesWithVisitorCountSnapshot(
    Dictionary<int, List<int>> CategoriesWithVisitorCount
);
