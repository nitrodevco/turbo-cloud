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

/// <summary>
/// Variable Management edited, gave or took away a variable on one user, pet or bot; the
/// packet's last field says which. The client is told whether it worked (it only shows a
/// failure) and is sent what the holder holds now: it shows a loading icon from the change
/// until new data comes and does not ask again itself (<c>VariableManagementDetailView</c>
/// <c>displayNewData</c>; inference: no official sample of the reply order yet).
/// </summary>
public class WiredSetUserPermanentVariableMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredSetUserPermanentVariableMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredSetUserPermanentVariableMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var success =
            WiredVariableId.TryParse(message.VariableId, out var variableId)
            && Enum.IsDefined((WiredVariableMenuOperationType)message.Operation)
            && await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .ApplyWiredVariableManagementChangeAsync(
                    ctx.AsActionContext(),
                    message.EntityType,
                    message.EntityId,
                    variableId,
                    (WiredVariableMenuOperationType)message.Operation,
                    new WiredVariableValue(message.Value),
                    ct
                )
                .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new WiredSetUserPermanentVariableResultMessageComposer { Success = success },
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendWiredEntityPermanentVariablesAsync(
                _grainFactory,
                message.EntityType,
                message.EntityId,
                ct
            )
            .ConfigureAwait(false);
    }
}
