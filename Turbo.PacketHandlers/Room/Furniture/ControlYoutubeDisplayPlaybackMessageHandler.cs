using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>Pause, play, next or previous on a video display. The command id comes from a client, so it is checked before it becomes an enum.</summary>
public class ControlYoutubeDisplayPlaybackMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ControlYoutubeDisplayPlaybackMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ControlYoutubeDisplayPlaybackMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || message.ObjectId <= 0
            || !Enum.IsDefined((YoutubePlaybackCommandType)message.CommandId)
        )
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithItemAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                new ControlYoutubePlaybackInteraction
                {
                    Command = (YoutubePlaybackCommandType)message.CommandId,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
