using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nux;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Nux;

/// <summary>
/// The new user gift picks (<c>HabboNuxDialogs.onSendGetGifts</c>), sent once every step of the
/// offer has one. Habbo's client waits for no answer: the gifts arrive in the inventory.
/// </summary>
public class NewUserExperienceGetGiftsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<NewUserExperienceGetGiftsMessage>
{
    public async ValueTask HandleAsync(
        NewUserExperienceGetGiftsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.Selections.IsEmpty)
            return;

        await grainFactory
            .GetPlayerNuxGiftGrain(ctx.PlayerId)
            .SelectAsync(message.Selections, ct)
            .ConfigureAwait(false);
    }
}
