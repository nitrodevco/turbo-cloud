namespace Turbo.Players.Grains;

/// <summary>The directory is one grain for the hotel, so its state carries no key.</summary>
internal sealed class PlayerDirectoryLiveState
{
    /// <summary>Id and name of recently asked-for players, bounded by config.</summary>
    public required PlayerNameCache Names { get; init; }
}
