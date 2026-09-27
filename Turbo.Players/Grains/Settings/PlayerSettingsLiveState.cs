using System.Collections.Frozen;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Settings;

namespace Turbo.Players.Grains.Settings;

internal sealed class PlayerSettingsLiveState
{
    public required PlayerId PlayerId { get; init; }
    public required PlayerSettingsSnapshot Settings { get; set; }

    /// <summary>Client ids of the purchasable chat styles the player owns; read once, on activation.</summary>
    public FrozenSet<int> OwnedChatStyleIds { get; set; } = FrozenSet<int>.Empty;

    /// <summary>Set by every change, cleared by the flush that writes it.</summary>
    public bool IsDirty { get; set; }
}
