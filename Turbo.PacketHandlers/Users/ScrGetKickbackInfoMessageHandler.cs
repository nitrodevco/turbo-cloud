using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// The club centre asks for its membership and HC Payday figures when it opens
/// (<c>HabboClubCenter.updateData</c>). Turbo pays no payday yet, so the credit figures are zero
/// and the countdown and share are the hotel's (<c>Turbo:Players:Subscriptions</c>); the join
/// date and the streak are the player's own.
/// </summary>
public class ScrGetKickbackInfoMessageHandler(
    IGrainFactory grainFactory,
    IOptions<PlayerConfig> playerConfig
) : IMessageHandler<ScrGetKickbackInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly SubscriptionConfig _config = playerConfig.Value.Subscriptions;

    public async ValueTask HandleAsync(
        ScrGetKickbackInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var club = await _grainFactory
            .GetPlayerSubscriptionGrain(ctx.PlayerId)
            .GetAsync(SubscriptionType.HabboClub, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ScrSendKickbackInfoMessageComposer
                {
                    // Only the run going on now: Habbo reads 0 a minute after a returning
                    // member's 31 days HC box starts (official client capture, 2026-10-13).
                    CurrentHcStreak = club.CurrentStreakDays,
                    FirstSubscriptionDate =
                        club.FirstSubscribedAt?.ToString(
                            _config.FirstSubscriptionDateFormat,
                            CultureInfo.InvariantCulture
                        ) ?? string.Empty,
                    KickbackPercentage = _config.PaydayKickbackPercentage,
                    TotalCreditsMissed = 0,
                    TotalCreditsRewarded = 0,
                    TotalCreditsSpent = 0,
                    CreditRewardForStreakBonus = 0,
                    CreditRewardForMonthlySpent = 0,
                    TimeUntilPayday = _config.MinutesUntilPayday,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
