using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
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
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var variables = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetAllVariablesForBindingAsync(
                new WiredVariableBinding()
                {
                    TargetType = (WiredVariableTargetType)message.SourceType,
                    TargetId = Math.Abs(message.SourceId),
                },
                ct
            )
            .ConfigureAwait(false);

        _ = ctx.SendComposerAsync(
                new WiredVariablesForObjectEventMessageComposer()
                {
                    TargetType = (WiredVariableTargetType)message.SourceType,
                    TargetId = message.SourceId,
                    VariableValues = variables,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
