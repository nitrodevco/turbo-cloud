using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Players.Onboarding;

namespace Turbo.PacketHandlers.Avatar;

/// <summary>
/// The own avatar menu's "change name" in a room (<c>NameChangeController.changeName</c>): the same
/// one name choice as the new user flow's, answered with <c>ChangeUserNameResult</c>. Anyone whose
/// name is already chosen is answered 6.
/// </summary>
public class ChangeUserNameInRoomMessageHandler(IPlayerOnboardingService onboarding)
    : IMessageHandler<ChangeUserNameInRoomMessage>
{
    public async ValueTask HandleAsync(
        ChangeUserNameInRoomMessage message,
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
