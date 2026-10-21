using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>Variable Management asked what one user, pet or bot holds.</summary>
public class WiredGetUserPermanentVariablesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetUserPermanentVariablesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetUserPermanentVariablesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        await ctx.SendWiredEntityPermanentVariablesAsync(
                _grainFactory,
                message.EntityType,
                message.EntityId,
                ct
            )
            .ConfigureAwait(false);
    }
}
