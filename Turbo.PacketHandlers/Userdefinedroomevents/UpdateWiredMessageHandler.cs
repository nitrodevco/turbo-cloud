using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// Saving a wired box is the same request for every kind of box; only the message type differs.
/// The room validates and stores the update, and the editor is told either way so it never
/// hangs on a save that was refused.
/// </summary>
public abstract class UpdateWiredMessageHandler<TMessage>(IGrainFactory grainFactory)
    : IMessageHandler<TMessage>
    where TMessage : UpdateWiredMessage
{
    private const string VALIDATION_ERROR_KEY = "wired.validation.error";

    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(TMessage message, MessageContext ctx, CancellationToken ct)
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.Id <= 0)
            return;

        var saved = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ApplyWiredUpdateAsync(ctx.AsActionContext(), message.Id, message, ct)
            .ConfigureAwait(false);

        if (!saved)
        {
            await ctx.SendComposerAsync(
                    new WiredValidationErrorEventMessageComposer
                    {
                        LocalizationKey = VALIDATION_ERROR_KEY,
                        Parameters = [],
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        await ctx.SendComposerAsync(new WiredSaveSuccessEventMessageComposer(), ct)
            .ConfigureAwait(false);
    }
}
