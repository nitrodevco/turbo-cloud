using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>Variable Management asked for a page of who holds a permanent user variable.</summary>
public class WiredGetVariableOwnersPageMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetVariableOwnersPageMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetVariableOwnersPageMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || !WiredVariableId.TryParse(message.VariableId, out var variableId)
        )
            return;

        var page = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredVariableOwnersPageAsync(
                ctx.AsActionContext(),
                variableId,
                message.Page,
                message.PageSize,
                message.SortType,
                message.UserTypeFilter,
                ct
            )
            .ConfigureAwait(false);

        if (page is null)
            return;

        await ctx.SendComposerAsync(new WiredUserVariablesListMessageComposer { Page = page }, ct)
            .ConfigureAwait(false);
    }
}
