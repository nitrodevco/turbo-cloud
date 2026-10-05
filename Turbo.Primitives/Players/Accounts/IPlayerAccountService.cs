using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Players.Accounts;

/// <summary>
/// Creating players. A player row is all a login needs: their wallet, settings and permissions
/// start empty and are made as they are first used, and the default group is everyone's.
/// </summary>
public interface IPlayerAccountService
{
    Task<NewPlayerResult> CreateAsync(NewPlayer player, CancellationToken ct);
}
