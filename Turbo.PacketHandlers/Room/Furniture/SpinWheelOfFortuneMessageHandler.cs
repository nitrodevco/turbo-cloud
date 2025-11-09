using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

public class SpinWheelOfFortuneMessageHandler : IMessageHandler<SpinWheelOfFortuneMessage>
{
    public async ValueTask HandleAsync(
        SpinWheelOfFortuneMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
