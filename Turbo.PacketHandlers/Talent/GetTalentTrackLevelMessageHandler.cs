using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Talent;

namespace Turbo.PacketHandlers.Talent;

public class GetTalentTrackLevelMessageHandler : IMessageHandler<GetTalentTrackLevelMessage>
{
    public async ValueTask HandleAsync(
        GetTalentTrackLevelMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
