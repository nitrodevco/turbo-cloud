using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Users;

public class RespectUserMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RespectUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RespectUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.UserId <= 0)
            return;

        // Cannot respect yourself
        if ((int)ctx.PlayerId == message.UserId)
            return;

        // Check if giver has respect left and consume it
        var giverGrain = _grainFactory.GetPlayerGrain(ctx.PlayerId);
        var consumed = await giverGrain.TryConsumeRespectAsync(ct).ConfigureAwait(false);

        if (!consumed)
            return;

        // Give respect to the target player
        var targetGrain = _grainFactory.GetPlayerGrain(message.UserId);
        var newTotal = await targetGrain
            .ReceiveRespectAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        // Play the thumbs-up expression on the giver
        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);
        await roomGrain
            .SetAvatarExpressionAsync(ctx.AsActionContext(), AvatarExpressionType.Respect, ct)
            .ConfigureAwait(false);

        // Broadcast the respect notification to the entire room
        await roomGrain
            .SendComposerToRoomAsync(
                new RespectNotificationMessageComposer
                {
                    UserId = message.UserId,
                    RespectTotal = newTotal,
                }
            )
            .ConfigureAwait(false);
    }
}
