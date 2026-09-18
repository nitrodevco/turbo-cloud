using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Stores a moodlight preset, and applies it when the client asks to.
/// </summary>
public class RoomDimmerSavePresetMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RoomDimmerSavePresetMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RoomDimmerSavePresetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithItemAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                new SaveDimmerPresetInteraction
                {
                    PresetId = message.PresetId,
                    EffectType = message.EffectType,
                    Color = message.Color,
                    Brightness = message.Brightness,
                    Apply = message.Apply,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
