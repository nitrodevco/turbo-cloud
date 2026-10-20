using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

internal static class WiredPermanentVariablesExtensions
{
    /// <summary>
    /// Sends Variable Management what one user, pet or bot holds. Asked for by its detail window,
    /// and sent again after a change there.
    /// </summary>
    public static async Task SendWiredEntityPermanentVariablesAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        int entityType,
        int entityId,
        CancellationToken ct
    )
    {
        var holder = await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredEntityPermanentVariablesAsync(ctx.AsActionContext(), entityType, entityId, ct)
            .ConfigureAwait(false);

        if (holder is null)
            return;

        await ctx.SendComposerAsync(
                new WiredUserPermanentVariablesMessageComposer { Holder = holder },
                ct
            )
            .ConfigureAwait(false);
    }
}
