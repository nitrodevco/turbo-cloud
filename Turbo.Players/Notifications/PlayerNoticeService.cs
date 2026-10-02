using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Texts;

namespace Turbo.Players.Notifications;

public sealed class PlayerNoticeService(
    IGrainFactory grainFactory,
    IHotelTextProvider texts,
    IOptions<PlayerConfig> config,
    ILogger<IPlayerNoticeService> logger
) : IPlayerNoticeService
{
    public Task<PlayerNoticeDelivery> SendAsync(
        PlayerId playerId,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    ) =>
        SendNoticeAsync(
            playerId,
            textKey,
            () =>
                new ModeratorMessageComposer
                {
                    Message = FormatText(textKey, defaultText, parameters),
                    Url = string.Empty,
                },
            ct
        );

    public Task<PlayerNoticeDelivery> SendCurrencyRewardAsync(
        PlayerId playerId,
        long amount,
        CurrencyTypeSnapshot currency,
        CancellationToken ct
    ) =>
        SendNoticeAsync(
            playerId,
            "player.reward.currency",
            () =>
                new NotificationDialogMessageComposer
                {
                    NotificationType = "currency_reward",
                    Parameters = ImmutableDictionary<string, string>
                        .Empty.Add("display", "BUBBLE")
                        .Add(
                            "message",
                            FormatText(
                                "player.reward.currency",
                                "You received %0% %1%.",
                                [amount.ToString(CultureInfo.InvariantCulture), currency.Name]
                            )
                        )
                        .Add("image", "if_icon_temp_png"),
                },
            ct
        );

    private string FormatText(string textKey, string defaultText, IReadOnlyList<string> parameters)
    {
        var message = texts.TryGetText(textKey, out var localized) ? localized : defaultText;
        for (var i = 0; i < parameters.Count; i++)
            message = message.Replace(
                $"%{i.ToString(CultureInfo.InvariantCulture)}%",
                parameters[i],
                StringComparison.Ordinal
            );
        return message;
    }

    private async Task<PlayerNoticeDelivery> SendNoticeAsync(
        PlayerId playerId,
        string textKey,
        Func<IComposer> createComposer,
        CancellationToken ct
    )
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(config.Value.NoticeTimeoutMs);
            var accepted = await grainFactory
                .TrySendComposerToPlayerAsync(playerId, createComposer(), deadline.Token)
                .WaitAsync(deadline.Token);
            return accepted ? PlayerNoticeDelivery.Sent : PlayerNoticeDelivery.Offline;
        }
        catch (Exception ex)
        {
            // The underlying mutation may already be committed. A notice must never change
            // its outcome or make callers repeat it. No message contents are logged.
            logger.LogWarning(
                ex,
                "Could not send notice {NoticeKey} to player {PlayerId}",
                textKey,
                playerId
            );
            return PlayerNoticeDelivery.Failed;
        }
    }
}
