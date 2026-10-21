using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Answers with the club info (ScrSendUserInfo) and, when club gifts are waiting, the
/// ClubGiftNotification Habbo sends right behind it (every one of 15 recorded answers to a
/// member); the account preferences answer GetSoundSettings.
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
        await _grainFactory
            .GetPlayerSubscriptionGrain(ctx.PlayerId)
            .SendClubInfoAsync(ScrUserInfoResponseType.Normal, ct)
            .ConfigureAwait(false);

        var gifts = await _grainFactory
            .GetCatalogPurchaseGrain(ctx.PlayerId)
            .GetClubGiftInfoAsync(ct)
            .ConfigureAwait(false);

        if (gifts is { GiftsAvailable: > 0 })
            await ctx.SendComposerAsync(
                    new ClubGiftNotificationEventMessageComposer
                    {
                        NumGifts = gifts.GiftsAvailable,
                    },
                    ct
                )
                .ConfigureAwait(false);
    }
}
