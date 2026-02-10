using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.PacketHandlers.Users;

public class ReplenishRespectMessageHandler(
    IGrainFactory grainFactory,
    IConfiguration configuration
) : IMessageHandler<ReplenishRespectMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IConfiguration _configuration = configuration;

    public async ValueTask HandleAsync(
        ReplenishRespectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var replenishCost = _configuration.GetValue("Turbo:Respect:ReplenishCostDuckets", 20);
        var maxRespectPerDay = _configuration.GetValue("Turbo:Respect:DailyRespectAmount", 3);

        // Deduct duckets via the wallet grain
        var walletGrain = _grainFactory.GetPlayerWalletGrain(ctx.PlayerId);
        var debitResult = await walletGrain
            .TryDebitAsync(
                [
                    new WalletDebitRequest
                    {
                        CurrencyKind = new CurrencyKind
                        {
                            CurrencyType = CurrencyType.ActivityPoints,
                            ActivityPointType = 0,
                        },
                        Amount = replenishCost,
                    },
                ],
                ct
            )
            .ConfigureAwait(false);

        if (!debitResult.Succeeded)
            return;

        // Replenish respect on the player grain
        var playerGrain = _grainFactory.GetPlayerGrain(ctx.PlayerId);
        var replenished = await playerGrain
            .TryReplenishRespectAsync(maxRespectPerDay, ct)
            .ConfigureAwait(false);

        if (!replenished)
            return;

        // Send full activity points snapshot so the client purse reflects the currency deduction
        var activityPoints = await walletGrain.GetActivityPointsAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ActivityPointsMessageComposer { PointsByCategoryId = activityPoints },
                ct
            )
            .ConfigureAwait(false);

        // Send updated UserObject to refresh the client's respect data
        var snapshot = await playerGrain.GetSummaryAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new UserObjectMessage { Player = snapshot, MaxRespectPerDay = maxRespectPerDay },
                ct
            )
            .ConfigureAwait(false);
    }
}
