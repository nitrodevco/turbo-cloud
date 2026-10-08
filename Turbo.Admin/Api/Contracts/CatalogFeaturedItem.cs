using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A featured item of the catalog's front page as saved: its place from 1, its title, its promo
/// picture's path, what it opens (<c>page</c>, <c>offer</c> or <c>product</c>, named by
/// <see cref="Value"/>) and when it comes off, null for never.
/// </summary>
public sealed record CatalogFeaturedItem(
    int Id,
    int Position,
    string Title,
    string Image,
    string Type,
    string Value,
    DateTime? ExpiresAtUtc
);
