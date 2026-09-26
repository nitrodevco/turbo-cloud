using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

internal static class WiredVariablesForObjectExtensions
{
    /// <summary>
    /// Sends the wired menu every variable one furni or user holds. Asked for when the inspect
    /// tab opens on a target, and sent again after an edit so it shows the new values.
    /// </summary>
    /// <param name="echoedTargetId">
    /// The id exactly as the client sent it. The binding carries it made positive, but the client
    /// matches the answer against the id it asked with.
    /// </param>
    public static async Task SendWiredVariablesForObjectAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        WiredVariableBinding binding,
        int echoedTargetId,
        CancellationToken ct
    )
    {
        var variables = await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetAllVariablesForBindingAsync(ctx.AsActionContext(), binding, ct)
            .ConfigureAwait(false);

        if (variables is null)
            return;

        await ctx.SendComposerAsync(
                new WiredVariablesForObjectEventMessageComposer
                {
                    TargetType = binding.TargetType,
                    TargetId = echoedTargetId,
                    VariableValues = variables,
                    ConfiguredInWireds = [],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
