using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Players.Onboarding;

namespace Turbo.PacketHandlers.Avatar;

/// <summary>The new user flow's name box asking whether a name is free.</summary>
public class CheckUserNameMessageHandler(IPlayerOnboardingService onboarding)
    : IMessageHandler<CheckUserNameMessage>
{
    public async ValueTask HandleAsync(
        CheckUserNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var check = await onboarding
            .CheckNameAsync(ctx.PlayerId, message.Name, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new CheckUserNameResultMessageComposer
                {
                    Result = check.Result,
                    Name = check.Name,
                    Suggestions = check.Suggestions,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
