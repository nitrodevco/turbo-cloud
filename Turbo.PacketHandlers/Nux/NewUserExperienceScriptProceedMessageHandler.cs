using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nux;

namespace Turbo.PacketHandlers.Nux;

public class NewUserExperienceScriptProceedMessageHandler
    : IMessageHandler<NewUserExperienceScriptProceedMessage>
{
    public async ValueTask HandleAsync(
        NewUserExperienceScriptProceedMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
