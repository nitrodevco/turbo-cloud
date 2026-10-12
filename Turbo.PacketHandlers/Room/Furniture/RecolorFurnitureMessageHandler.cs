using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Recolors a recolorable furni from the recolor window, with the other furni its scope names.
/// </summary>
public class RecolorFurnitureMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RecolorFurnitureMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RecolorFurnitureMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (!Enum.IsDefined(message.Scope))
            return;

        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new RecolorFurnitureInteraction
                {
                    Scope = message.Scope,
                    Red = message.Red,
                    Green = message.Green,
                    Blue = message.Blue,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
