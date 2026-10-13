using System.Collections.Frozen;
using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Settings;

namespace Turbo.Players.Grains.Settings;

internal sealed class PlayerSettingsLiveState
{
    public required PlayerId PlayerId { get; init; }
    public required PlayerSettingsSnapshot Settings { get; set; }

    /// <summary>Client ids of the purchasable chat styles the player owns; read once, on activation.</summary>
    public FrozenSet<int> OwnedChatStyleIds { get; set; } = FrozenSet<int>.Empty;

    /// <summary>The player's own word filter, read the first time it is asked for.</summary>
    public List<string>? CustomFilterWords { get; set; }

    /// <summary>Set by every change, cleared by the flush that writes it.</summary>
    public bool IsDirty { get; set; }
}
