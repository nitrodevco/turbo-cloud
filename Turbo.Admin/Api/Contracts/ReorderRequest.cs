using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Ids in the order they go in.</summary>
public sealed record ReorderRequest(List<int>? Ids);
