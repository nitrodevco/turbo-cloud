using System;
using Orleans;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// A bonus rare campaign: every <see cref="CreditsRequired"/> credits a player brings in gives
/// them the furniture. The reception's bonus rare widget shows the one running.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record BonusRareCampaignSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    /// <summary>What progress is kept under: a new code starts everyone afresh.</summary>
    [Id(1)]
    public required string Code { get; init; }

    /// <summary>The furniture definition given, by name.</summary>
    [Id(2)]
    public required string FurnitureName { get; init; }

    /// <summary>The product data code the widget names the reward by.</summary>
    [Id(3)]
    public required string ProductCode { get; init; }

    [Id(4)]
    public required int CreditsRequired { get; init; }

    [Id(5)]
    public required BonusRareSource Source { get; init; }

    /// <summary>When it starts (UTC). The last one started and not ended runs.</summary>
    [Id(6)]
    public required DateTime StartsAt { get; init; }

    /// <summary>When it ends (UTC); null for never.</summary>
    [Id(7)]
    public DateTime? EndsAt { get; init; }

    public bool IsRunning(DateTime now) => StartsAt <= now && (EndsAt is null || EndsAt > now);
}
