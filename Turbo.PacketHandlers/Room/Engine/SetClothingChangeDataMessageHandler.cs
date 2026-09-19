using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Orleans;
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
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || message.ObjectId <= 0
            || string.IsNullOrEmpty(message.Figure)
        )
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithItemAsync(
                ctx.AsActionContext(),
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
