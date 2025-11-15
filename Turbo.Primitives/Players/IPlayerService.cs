namespace Turbo.Primitives.Players;

public interface IPlayerService
{
    public IPlayerGrain GetPlayerGrain(long playerId);
}
