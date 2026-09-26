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
/// The wired menu edited a variable's value on one furni or user, gave it the variable or took
/// the variable away; the packet's last field says which. The room checks permission, applies
/// it and answers with the refreshed value list of that target.
/// </summary>
public class WiredSetObjectVariableValueMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredSetObjectVariableValueMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredSetObjectVariableValueMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || !WiredVariableId.TryParse(message.VariableId, out var variableId)
            || !Enum.IsDefined((WiredVariableMenuOperationType)message.Operation)
        )
            return;

        var binding = new WiredVariableBinding(
            (WiredVariableTargetType)message.VariableTarget,
            Math.Abs(message.ObjectIdForType)
        );
        var room = _grainFactory.GetRoomGrain(ctx.RoomId);

        if (
            !await room.ApplyWiredVariableMenuOperationAsync(
                    ctx.AsActionContext(),
                    binding,
                    variableId,
                    (WiredVariableMenuOperationType)message.Operation,
                    new WiredVariableValue(message.Value),
                    ct
                )
                .ConfigureAwait(false)
        )
            return;

        var variables = await room.GetAllVariablesForBindingAsync(
                ctx.AsActionContext(),
                binding,
                ct
            )
            .ConfigureAwait(false);

        if (variables is null)
            return;

        await ctx.SendComposerAsync(
                new WiredVariablesForObjectEventMessageComposer
                {
                    TargetType = binding.TargetType,
                    TargetId = message.ObjectIdForType,
                    VariableValues = variables,
                    ConfiguredInWireds = [],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
