using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// Clears the monitor's error counts and confirms with the list, now all at zero, so the open tab
/// refreshes: the list always holds the same ten errors.
/// </summary>
public class WiredClearErrorLogsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredClearErrorLogsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredClearErrorLogsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var cleared = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ClearWiredErrorLogsAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (!cleared)
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
