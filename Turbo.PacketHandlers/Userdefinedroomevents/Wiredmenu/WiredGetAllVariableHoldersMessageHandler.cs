using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>The wired menu asked which furni or users hold a value for one variable.</summary>
public class WiredGetAllVariableHoldersMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetAllVariableHoldersMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetAllVariableHoldersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || !WiredVariableId.TryParse(message.SelectedVariableId, out var variableId)
        )
            return;

        await ctx.SendWiredVariableHoldersAsync(_grainFactory, variableId, ct)
            .ConfigureAwait(false);
    }
}
