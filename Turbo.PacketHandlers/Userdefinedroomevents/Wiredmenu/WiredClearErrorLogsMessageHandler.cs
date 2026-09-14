using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// Clears the monitor's error list and confirms with an empty list so the open tab refreshes.
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

        await ctx.SendComposerAsync(
                new WiredErrorLogsEventMessageComposer
                {
                    Errors = ImmutableArray<WiredErrorLogSnapshot>.Empty,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
