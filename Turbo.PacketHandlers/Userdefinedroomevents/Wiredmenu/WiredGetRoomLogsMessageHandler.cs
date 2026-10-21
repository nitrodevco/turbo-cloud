using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>A page of the room's wired log ("View full logs"), for whoever may read its wired.</summary>
public class WiredGetRoomLogsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetRoomLogsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetRoomLogsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        var page = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredRoomLogsAsync(
                ctx.AsActionContext(),
                message.Page,
                message.PageSize,
                message.LogLevelFilter,
                message.LogSourceFilter,
                message.Query,
                ct
            )
            .ConfigureAwait(false);

        if (page is null)
            return;

        await ctx.SendComposerAsync(new WiredRoomLogsMessageComposer { Page = page }, ct)
            .ConfigureAwait(false);
    }
}
