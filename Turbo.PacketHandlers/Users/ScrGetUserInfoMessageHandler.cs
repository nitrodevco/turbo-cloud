using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Answers with the club info only (ScrSendUserInfo), as Habbo does; the account preferences
/// answer GetSoundSettings.
/// </summary>
public class ScrGetUserInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ScrGetUserInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ScrGetUserInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSubscriptionGrain(ctx.PlayerId)
            .SendClubInfoAsync(ScrUserInfoResponseType.Normal, ct)
            .ConfigureAwait(false);
    }
}
