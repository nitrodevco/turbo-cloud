using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetAllVariablesHashMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetAllVariablesHashMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetAllVariablesHashMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var variables = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredVariablesSnapshotAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (variables is null)
            return;

        await ctx.SendComposerAsync(
                new WiredAllVariablesHashEventMessageComposer()
                {
                    AllVariablesHash = variables.AllVariablesHash,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
