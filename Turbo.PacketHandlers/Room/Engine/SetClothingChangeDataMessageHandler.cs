using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Room.Engine;

/// <summary>The avatar editor dressed a clothing booth instead of the player: the look for one gender.</summary>
public class SetClothingChangeDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetClothingChangeDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetClothingChangeDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(message.Figure))
            return;

        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetClothingChangeInteraction
                {
                    Gender = AvatarGenderTypeExtensions.FromLegacyString(message.Gender),
                    Figure = message.Figure,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
