using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetAllVariablesDiffsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetAllVariablesDiffsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetAllVariablesDiffsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        _ = ctx.SendComposerAsync(
                new WiredAllVariablesDiffsEventMessageComposer()
                {
                    AllVariablesHash = 0,
                    IsLastChunk = true,
                    ChunkIndex = 0,
                    RemovedVariables = [],
                    AddedOrUpdated = [],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
