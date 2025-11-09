using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Furniture.Configuration;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Rooms.Abstractions;

namespace Turbo.PacketHandlers.Room;

public class GetHeightMapMessageHandler(
    IRoomService roomService,
    IOptions<FurnitureConfig> furnitureConfig
) : IMessageHandler<GetHeightMapMessage>
{
    private readonly IRoomService _roomService = roomService;
    private readonly FurnitureConfig _furnitureConfig = furnitureConfig.Value;

    public async ValueTask HandleAsync(
        GetHeightMapMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _roomService
            .EnterPendingRoomForPlayerIdAsync(ctx.Session.PlayerId, ct)
            .ConfigureAwait(false);
    }
}
