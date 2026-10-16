using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// The client asks for the message of the day once its notifications start
/// (<c>HabboNotifications.activate</c>). It is the welcome message staff set in the admin panel;
/// while that is empty nothing is answered, as the official hotel answers nothing.
/// </summary>
public class GetMOTDMessageHandler(IGrainFactory grainFactory) : IMessageHandler<GetMOTDMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetMOTDMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var welcomeMessage = await _grainFactory
            .GetWelcomeMessageGrain()
            .GetMessageAsync(ct)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(welcomeMessage))
            return;

        await ctx.SendComposerAsync(
                new MOTDNotificationEventMessageComposer { Messages = [welcomeMessage] },
                ct
            )
            .ConfigureAwait(false);
    }
}
