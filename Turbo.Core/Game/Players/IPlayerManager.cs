using System.Threading.Tasks;
using Turbo.Core.Contracts.Players;

namespace Turbo.Core.Game.Players;

public interface IPlayerManager
{
    public Task<IPlayerGrain> GetPlayerGrain(long playerId);

    public Task<bool> PlayerExistsAsync(long playerId);
}
