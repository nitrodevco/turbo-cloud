using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Players.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Trading;
using Turbo.Primitives.Trading.Enums;

namespace Turbo.Players.Trading;

/// <summary>
/// The hotel's trade rules for one player: the <c>trade</c> permission (a trade lock takes it),
/// then the trading pass as <see cref="TradingConfig.PassMode"/> asks for it.
/// </summary>
public sealed class PlayerTradePolicy(
    IGrainFactory grainFactory,
    IOptionsMonitor<TradingConfig> tradingConfig
) : IPlayerTradePolicy
{
    public async Task<TradeStandingType> GetTradeStandingAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        if (!await grainFactory.HasPermissionAsync(playerId, PermissionNodes.TRADE, ct))
            return TradeStandingType.Locked;

        var config = tradingConfig.CurrentValue;

        if (await grainFactory.HoldsTradingPassAsync(config, playerId, ct))
            return TradeStandingType.Full;

        return config.PassMode == TradingPassMode.ReceiveOnly
            ? TradeStandingType.ReceiveOnly
            : TradeStandingType.NoPass;
    }
}
