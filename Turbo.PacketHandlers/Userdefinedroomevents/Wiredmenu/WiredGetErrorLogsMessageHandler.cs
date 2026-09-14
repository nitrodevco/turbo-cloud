using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetErrorLogsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetErrorLogsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetErrorLogsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var errors = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredErrorLogsAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (errors is null)
            return;

        await ctx.SendComposerAsync(
                new WiredErrorLogsEventMessageComposer { Errors = errors.Value },
                ct
            )
            .ConfigureAwait(false);
    }
}
