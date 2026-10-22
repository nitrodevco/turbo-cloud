using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Games.Grains;

/// <summary>
/// The game centre, one grain for the hotel: its open lobbies, their countdowns and the games
/// they start. It sends the lobby messages itself.
/// </summary>
public interface IGameDirectoryGrain : IGrainWithStringKey
{
    public Task<GameDirectoryStatusSnapshot> GetDirectoryStatusAsync(
        PlayerId playerId,
        CancellationToken ct
    );

    public Task<GameAccountStatusSnapshot> GetAccountStatusAsync(
        PlayerId playerId,
        int gameTypeId,
        CancellationToken ct
    );

    /// <summary>Joins the game named <paramref name="key"/>, or the default game when null.</summary>
    public Task JoinAsync(PlayerId playerId, string? key, CancellationToken ct);

    public Task LeaveAsync(PlayerId playerId, CancellationToken ct);
}
