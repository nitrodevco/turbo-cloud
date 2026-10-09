using System;
using Turbo.Primitives.Hotel.Enums;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A promo article as staff write it.</summary>
public sealed record PromoArticleRequest(
    string? Title,
    string? BodyText,
    string? ButtonText,
    PromoArticleLinkType? LinkType,
    string? LinkContent,
    string? ImageUrl,
    bool? Visible,
    DateTime? StartsAt,
    DateTime? EndsAt
);
