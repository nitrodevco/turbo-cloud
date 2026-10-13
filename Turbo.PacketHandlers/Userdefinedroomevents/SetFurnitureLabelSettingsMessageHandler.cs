using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>A WIRED label's settings window saved: bubble colour, audience, always open.</summary>
public class SetFurnitureLabelSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetFurnitureLabelSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetFurnitureLabelSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (!Enum.IsDefined(message.Visibility))
            return;

        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetFurnitureLabelSettingsInteraction
                {
                    BubbleColor = message.BubbleColor,
                    Visibility = message.Visibility,
                    AlwaysOpen = message.AlwaysOpen,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
