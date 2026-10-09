using System;
using Orleans;
using Turbo.Primitives.Hotel.Enums;

namespace Turbo.Primitives.Hotel.Snapshots;

/// <summary>
/// A promo article: one page of the reception's article carousel (<c>PromoArticleWidget</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PromoArticleSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Title { get; init; }

    [Id(2)]
    public required string BodyText { get; init; }

    [Id(3)]
    public required string ButtonText { get; init; }

    [Id(4)]
    public required PromoArticleLinkType LinkType { get; init; }

    /// <summary>The address or client link the button opens.</summary>
    [Id(5)]
    public required string LinkContent { get; init; }

    /// <summary>The picture's path under the client's <c>image.library.url</c>; empty for none.</summary>
    [Id(6)]
    public required string ImageUrl { get; init; }

    /// <summary>Its place in the carousel: lowest first.</summary>
    [Id(7)]
    public required int SortOrder { get; init; }

    /// <summary>Whether players see it at all.</summary>
    [Id(8)]
    public required bool Visible { get; init; }

    /// <summary>When players start seeing it (UTC); null for at once.</summary>
    [Id(9)]
    public DateTime? StartsAt { get; init; }

    /// <summary>When players stop seeing it (UTC); null for never.</summary>
    [Id(10)]
    public DateTime? EndsAt { get; init; }

    /// <summary>Whether players see it at <paramref name="now"/>.</summary>
    public bool IsLive(DateTime now) =>
        Visible && (StartsAt is null || StartsAt <= now) && (EndsAt is null || EndsAt > now);
}
