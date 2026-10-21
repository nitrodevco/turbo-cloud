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
/// failure), then sent what the holder holds now unasked, as Habbo does (official client
/// capture, WIN63-202610201032: result, then WiredUserPermanentVariables ~70 ms later, for a
/// give and a remove); the client keeps a loading icon from the change until new data comes
/// (<c>VariableManagementDetailView.displayNewData</c>). The holder list behind it is not
/// refreshed. Only successes were recorded; a failure is answered the same way (inference).
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
        if (ctx.RoomId <= 0)
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
