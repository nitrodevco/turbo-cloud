using System;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A bonus rare campaign as staff write it; times are UTC.</summary>
public sealed record BonusRareCampaignRequest(
    string? Code,
    string? FurnitureName,
    string? ProductCode,
    int? CreditsRequired,
    BonusRareSource? Source,
    DateTime? StartsAt,
    DateTime? EndsAt
);
