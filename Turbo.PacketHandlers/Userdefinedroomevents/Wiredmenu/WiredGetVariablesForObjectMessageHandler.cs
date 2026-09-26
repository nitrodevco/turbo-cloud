using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetVariablesForObjectMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetVariablesForObjectMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetVariablesForObjectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await ctx.SendWiredVariablesForObjectAsync(
                _grainFactory,
                new WiredVariableBinding(
                    (WiredVariableTargetType)message.SourceType,
                    Math.Abs(message.SourceId)
                ),
                message.SourceId,
                ct
            )
            .ConfigureAwait(false);
    }
}
