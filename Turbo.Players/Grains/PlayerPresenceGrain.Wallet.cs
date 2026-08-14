using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
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
                await SendComposerAsync(
                    new CreditBalanceEventMessageComposer { Balance = snapshot.Amount.ToString() }
                );
                break;
            case CurrencyType.Emeralds:
                await SendComposerAsync(
                    new EmeraldBalanceMessageComposer { EmeraldBalance = snapshot.Amount }
                );
                break;
            case CurrencyType.Silver:
                await SendComposerAsync(
                    new SilverBalanceMessageComposer { SilverBalance = snapshot.Amount }
                );
                break;
            case CurrencyType.ActivityPoints:
                await SendComposerAsync(
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
