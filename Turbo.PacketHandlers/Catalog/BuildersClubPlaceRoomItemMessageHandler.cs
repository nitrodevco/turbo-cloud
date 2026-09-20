using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Catalog;

public class BuildersClubPlaceRoomItemMessageHandler(IRoomService roomService)
    : IMessageHandler<BuildersClubPlaceRoomItemMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        BuildersClubPlaceRoomItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _roomService
            .PlaceBuildersClubFloorItemInRoomAsync(
                ctx.AsActionContext(),
                message.PageId,
                message.OfferId,
                message.ExtraParam ?? string.Empty,
                message.X,
                message.Y,
                (Rotation)message.Direction,
                message.ConfirmedHideRoom,
                ct
            )
            .ConfigureAwait(false);
    }
}
