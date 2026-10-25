using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Primitives.Games;

/// <summary>
/// How many games a player has left, asked before a join and shown in the game centre. The
/// default reads <c>Turbo:Games</c>; a plugin replaces it by declaring a public class implementing
/// it, or by registering one in its services (<c>docs/plugin-services.md</c>).
/// </summary>
[ReplaceableService]
public interface IGameAccessPolicy
{
    /// <summary>Games left to start now; <see cref="GameCenter.UNLIMITED"/> for no limit.</summary>
    public Task<int> GetFreeGamesLeftAsync(PlayerId playerId, int gameTypeId, CancellationToken ct);
}
