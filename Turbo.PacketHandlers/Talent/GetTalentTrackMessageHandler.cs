using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Talent;

namespace Turbo.PacketHandlers.Talent;

public class GetTalentTrackMessageHandler : IMessageHandler<GetTalentTrackMessage>
{
    public async ValueTask HandleAsync(
        GetTalentTrackMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
