namespace Turbo.Core.Game.Players;

using System.Threading.Tasks;

using Turbo.Core.Contracts.Players;

public interface IPlayerManager
{
    public Task<IPlayerGrain> GetPlayerGrain(long playerId);

    public Task<bool> PlayerExistsAsync(long playerId);
}
