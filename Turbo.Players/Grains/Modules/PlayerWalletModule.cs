using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains.Modules;

internal sealed class PlayerWalletModule(PlayerPresenceGrain presenceGrain)
{
    private readonly PlayerPresenceGrain _presenceGrain = presenceGrain;

    public async Task OnCurrencyUpdateAsync(
        WalletCurrencyUpdateSnapshot snapshot,
        CancellationToken ct
    )
    {
        if (snapshot is null)
            return;

        switch (snapshot.CurrencyKind.CurrencyType)
        {
            case CurrencyType.Credits:
                await _presenceGrain.SendComposerAsync(
                    new CreditBalanceEventMessageComposer { Balance = snapshot.Amount.ToString() }
                );
                break;
            case CurrencyType.Emeralds:
                await _presenceGrain.SendComposerAsync(
                    new EmeraldBalanceMessageComposer { EmeraldBalance = snapshot.Amount }
                );
                break;
            case CurrencyType.Silver:
                await _presenceGrain.SendComposerAsync(
                    new SilverBalanceMessageComposer { SilverBalance = snapshot.Amount }
                );
                break;
            case CurrencyType.ActivityPoints:
                await _presenceGrain.SendComposerAsync(
                    new HabboActivityPointNotificationMessageComposer
                    {
                        Amount = snapshot.Amount,
                        Change = snapshot.ChangedBy,
                        ActivityPointType = snapshot.CurrencyKind.ActivityPointType ?? -1,
                    }
                );
                break;
        }
    }
}
