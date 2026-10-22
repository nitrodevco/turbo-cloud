using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Players.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Trading.Enums;

namespace Turbo.Players.Trading;

public static class TradingPassExtensions
{
    /// <summary>
    /// Whether the player may give items away as the hotel's pass rule stands: always when no
    /// pass is asked for, otherwise when they own <see cref="TradingConfig.PassBadgeCode"/>.
    /// The <c>trade</c> permission (a trade lock) is checked apart from this.
    /// </summary>
    public static async Task<bool> HoldsTradingPassAsync(
        this IGrainFactory factory,
        TradingConfig config,
        PlayerId playerId,
        CancellationToken ct
    ) =>
        config.PassMode == TradingPassMode.Off
        || await factory.GetPlayerBadgeGrain(playerId).HasBadgeAsync(config.PassBadgeCode, ct);
}
