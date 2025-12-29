using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateTriggerMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateTriggerMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateTriggerMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.Id <= 0)
            return;

        if (
            !await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .ApplyWiredUpdateAsync(ctx.AsActionContext(), message.Id, message, ct)
                .ConfigureAwait(false)
        )
        {
            _ = ctx.SendComposerAsync(
                    new WiredValidationErrorEventMessageComposer
                    {
                        LocalizationKey = "wired.validation.error",
                        Parameters = [],
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        _ = ctx.SendComposerAsync(new WiredSaveSuccessEventMessageComposer(), ct)
            .ConfigureAwait(false);
    }
}
