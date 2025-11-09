using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Avatareffect;

namespace Turbo.PacketHandlers.Inventory.Avatareffect;

public class AvatarEffectActivatedMessageHandler : IMessageHandler<AvatarEffectActivatedMessage>
{
    public async ValueTask HandleAsync(
        AvatarEffectActivatedMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
