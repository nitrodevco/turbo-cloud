using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nux;

namespace Turbo.PacketHandlers.Nux;

public class NewUserExperienceGetGiftsMessageHandler
    : IMessageHandler<NewUserExperienceGetGiftsMessage>
{
    public async ValueTask HandleAsync(
        NewUserExperienceGetGiftsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
