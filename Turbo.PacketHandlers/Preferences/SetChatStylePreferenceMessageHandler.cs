using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;

namespace Turbo.PacketHandlers.Preferences;

public class SetChatStylePreferenceMessageHandler : IMessageHandler<SetChatStylePreferenceMessage>
{
    public async ValueTask HandleAsync(
        SetChatStylePreferenceMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
