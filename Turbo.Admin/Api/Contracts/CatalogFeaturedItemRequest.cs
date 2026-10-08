using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A featured item as the editor saves it: <see cref="Type"/> is <c>page</c>, <c>offer</c> or
/// <c>product</c>, and <see cref="Value"/> the page's name, the offer's id or the product code.
/// </summary>
public sealed record CatalogFeaturedItemRequest(
    string? Title,
    string? Image,
    string? Type,
    string? Value,
    DateTime? ExpiresAtUtc
);
