using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Players.Notifications;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerWalletGrain
{
    public async Task DeliverPendingRewardsAsync(CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
            var pending = await dbCtx
                .PlayerCurrencies.Where(x =>
                    x.PlayerEntityId == PlayerId.Value && x.PendingRewardAmount > 0
                )
                .OrderBy(x => x.Id)
                .ToListAsync(ct);

            foreach (var reward in pending)
            {
                var currency = _currencyTypeProvider.GetCurrencyType(reward.CurrencyTypeEntityId);
                if (currency is null)
                    continue;

                var delivery = await _noticeService.SendCurrencyRewardAsync(
                    PlayerId,
                    reward.PendingRewardAmount,
                    currency,
                    ct
                );
                if (delivery != PlayerNoticeDelivery.Sent)
                    break;

                // Wallet turns are serialized. Clear only after submission; a failed send or an
                // offline player keeps the receipt. A crash between send and save can replay it,
                // but delivering a receipt never changes the balance.
                reward.PendingRewardAmount = 0;
                await dbCtx.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not deliver pending currency rewards for player {PlayerId}",
                PlayerId
            );
        }
    }
}
