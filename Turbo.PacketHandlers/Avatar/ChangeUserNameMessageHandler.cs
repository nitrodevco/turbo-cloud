using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Players.Onboarding;

namespace Turbo.PacketHandlers.Avatar;

/// <summary>
/// The new user flow's "I'm ready": the name the player chose, once. Anyone past that step is
/// answered 6, as Habbo does.
/// </summary>
public class ChangeUserNameMessageHandler(IPlayerOnboardingService onboarding)
    : IMessageHandler<ChangeUserNameMessage>
{
    public async ValueTask HandleAsync(
        ChangeUserNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var change = await onboarding
            .ChooseNameAsync(ctx.PlayerId, message.Name, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ChangeUserNameResultMessageComposer
                {
                    Result = change.Result,
                    Name = change.Name,
                    Suggestions = change.Suggestions,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
