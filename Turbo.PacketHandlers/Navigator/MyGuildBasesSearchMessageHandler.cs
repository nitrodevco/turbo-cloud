using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class MyGuildBasesSearchMessageHandler : IMessageHandler<MyGuildBasesSearchMessage>
{
    public async ValueTask HandleAsync(
        MyGuildBasesSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
