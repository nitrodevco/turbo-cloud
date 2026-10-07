using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Saves what a chest's owner wants to be told about it.</summary>
public class SetChestNotificationPreferencesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetChestNotificationPreferencesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetChestNotificationPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new SetChestNotificationPreferencesInteraction
                {
                    NotifyMode = (WiredChestNotifyMode)message.NotifyMode,
                    OnChestFull = message.NotifyOnChestFull,
                    OnDonation = message.NotifyOnDonation,
                    OnWithdraw = message.NotifyOnWithdraw,
                    OnChestEmpty = message.NotifyOnChestEmpty,
                    OnWiredTransaction = message.NotifyOnWiredTransaction,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
