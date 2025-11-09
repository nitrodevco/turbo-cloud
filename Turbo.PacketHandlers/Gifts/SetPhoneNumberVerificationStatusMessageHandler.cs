using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Gifts;

namespace Turbo.PacketHandlers.Gifts;

public class SetPhoneNumberVerificationStatusMessageHandler
    : IMessageHandler<SetPhoneNumberVerificationStatusMessage>
{
    public async ValueTask HandleAsync(
        SetPhoneNumberVerificationStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
