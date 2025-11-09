using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Pets;

namespace Turbo.PacketHandlers.Room.Pets;

public class BreedPetsMessageHandler : IMessageHandler<BreedPetsMessage>
{
    public async ValueTask HandleAsync(
        BreedPetsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
