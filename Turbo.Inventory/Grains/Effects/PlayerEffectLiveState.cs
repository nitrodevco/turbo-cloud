using System;
using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Inventory.Grains.Effects;

/// <summary>What a player's effect grain holds: every effect they own, by effect id.</summary>
internal sealed class PlayerEffectLiveState
{
    public required PlayerId PlayerId { get; init; }

    public Dictionary<int, OwnedEffect> EffectsById { get; } = [];
}

/// <summary>
/// An effect row as the grain keeps it. It changes in place, always after the row has been
/// written, so memory never holds something the database does not.
/// </summary>
internal sealed class OwnedEffect(int rowId, int effectId)
{
    public int RowId { get; } = rowId;

    public int EffectId { get; } = effectId;

    public int SubType { get; set; }

    /// <summary>Copies waiting to be activated; the running copy is not counted.</summary>
    public int InactiveCount { get; set; }

    public bool IsPermanent { get; set; }

    /// <summary>When the running copy runs out (UTC); null when none runs.</summary>
    public DateTime? ExpiresAt { get; set; }
}
