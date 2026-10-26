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

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// The club centre asks for its membership and HC Payday figures when it opens
/// (<c>HabboClubCenter.updateData</c>): the player's streak and join date, what they spent in the
/// catalogue towards the coming payday and what that earns them, and the time left until it.
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
        var payday = await _grainFactory
            .GetPlayerSubscriptionGrain(ctx.PlayerId)
            .GetHcPaydayAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ScrSendKickbackInfoMessageComposer
                {
                    // Only the run going on now: Habbo reads 0 a minute after a returning
                    // member's 31 days HC box starts (official client capture, 2026-10-13).
                    CurrentHcStreak = payday.CurrentStreakDays,
                    FirstSubscriptionDate =
                        payday.FirstSubscribedAt?.ToString(
                            _config.FirstSubscriptionDateFormat,
                            CultureInfo.InvariantCulture
                        ) ?? string.Empty,
                    KickbackPercentage = payday.KickbackPercentage,
                    TotalCreditsMissed = payday.CreditsMissed,
                    TotalCreditsRewarded = payday.CreditsRewarded,
                    TotalCreditsSpent = payday.CreditsSpent,
                    CreditRewardForStreakBonus = payday.StreakBonusCredits,
                    CreditRewardForMonthlySpent = payday.SpendRewardCredits,
                    TimeUntilPayday = payday.MinutesUntilPayday,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
