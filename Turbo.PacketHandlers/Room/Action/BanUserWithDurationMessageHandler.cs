using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Action;

/// <summary>
/// Bans a player for an hour, a day or permanently, as the avatar menu offers.
/// </summary>
public class BanUserWithDurationMessageHandler(IRoomService roomService)
    : IMessageHandler<BanUserWithDurationMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        BanUserWithDurationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || message.UserId <= 0
            || message.RoomId != ctx.RoomId
            || !RoomBanTypes.TryParse(message.BanType, out var duration)
        )
            return;

        await _roomService
            .BanPlayerAsync(ctx.AsActionContext(), message.UserId, duration, ct)
            .ConfigureAwait(false);
    }
}
