using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;

namespace Turbo.PacketHandlers.RoomSettings;

public class UpdateRoomCategoryAndTradeSettingsMessageHandler
    : IMessageHandler<UpdateRoomCategoryAndTradeSettingsMessage>
{
    public async ValueTask HandleAsync(
        UpdateRoomCategoryAndTradeSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
