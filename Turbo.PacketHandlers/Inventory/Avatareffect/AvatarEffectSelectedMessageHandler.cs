using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Avatareffect;

namespace Turbo.PacketHandlers.Inventory.Avatareffect;

public class AvatarEffectSelectedMessageHandler : IMessageHandler<AvatarEffectSelectedMessage>
{
    public async ValueTask HandleAsync(
        AvatarEffectSelectedMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
