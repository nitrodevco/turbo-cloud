using System.Collections.Immutable;
using Turbo.Primitives.Players;

namespace Turbo.Players.Grains;

/// <summary>The directory is one grain for the hotel, so its state carries no key.</summary>
internal sealed class PlayerDirectoryLiveState
{
    /// <summary>Id and name of recently asked-for players, bounded by config.</summary>
    public required PlayerNameCache Names { get; init; }

    /// <summary>The system accounts, read once and dropped on each change; few, so all are held.</summary>
    public ImmutableArray<PlayerId>? SystemAccountIds { get; set; }

    /// <summary>Moves on each change, so a read that ran across one does not keep what it saw.</summary>
    public int SystemAccountsVersion { get; set; }
}
