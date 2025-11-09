using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Register;

namespace Turbo.PacketHandlers.Register;

public class UpdateFigureDataMessageHandler : IMessageHandler<UpdateFigureDataMessage>
{
    public async ValueTask HandleAsync(
        UpdateFigureDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
