using System;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>A catalog page the reception's expiring page widget counts down to.</summary>
[GenerateSerializer, Immutable]
public sealed record CatalogPageExpirySnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int PageId { get; init; }

    /// <summary>The page's name: what the client opens it by, and finds its texts and teaser by.</summary>
    [Id(2)]
    public required string PageName { get; init; }

    /// <summary>When it runs out (UTC).</summary>
    [Id(3)]
    public required DateTime ExpiresAt { get; init; }

    [Id(4)]
    public required string Image { get; init; }
}
