using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
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
    IDbContextFactory<TurboDbContext> dbCtxFactory,
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
            async token => new ModeratorMessageComposer
            {
                Message = await FormatTextAsync(textKey, defaultText, parameters, token)
                    .ConfigureAwait(false),
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
            async token => new NotificationDialogMessageComposer
            {
                NotificationType = "currency_reward",
                Parameters = ImmutableDictionary<string, string>
                    .Empty.Add("display", "BUBBLE")
                    .Add(
                        "message",
                        await FormatTextAsync(
                                "player.reward.currency",
                                "You received %0% %1%.",
                                [amount.ToString(CultureInfo.InvariantCulture), currency.Name],
                                token
                            )
                            .ConfigureAwait(false)
                    )
                    .Add("image", "if_icon_temp_png"),
            },
            ct
        );

    public async Task<PlayerNoticeDelivery> SendPersistentAsync(
        PlayerId playerId,
        string notificationType,
        ImmutableDictionary<string, string> parameters,
        CancellationToken ct
    )
    {
        var delivery = await SendNoticeAsync(
                playerId,
                notificationType,
                _ =>
                    Task.FromResult<IComposer>(
                        new NotificationDialogMessageComposer
                        {
                            NotificationType = notificationType,
                            Parameters = parameters,
                        }
                    ),
                ct
            )
            .ConfigureAwait(false);

        if (delivery != PlayerNoticeDelivery.Offline || config.Value.PersistentNoticeLimit <= 0)
            return delivery;

        try
        {
            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

            dbCtx.PlayerPendingNotifications.Add(
                new PlayerPendingNotificationEntity
                {
                    PlayerEntityId = playerId.Value,
                    NotificationType = notificationType,
                    Parameters = JsonSerializer.Serialize(parameters),
                }
            );
            await dbCtx.SaveChangesAsync(ct);

            // Only the newest are kept: what falls past the limit would never be shown.
            var dropped = await dbCtx
                .PlayerPendingNotifications.Where(x => x.PlayerEntityId == playerId.Value)
                .OrderByDescending(x => x.Id)
                .Skip(config.Value.PersistentNoticeLimit)
                .ToListAsync(ct);

            if (dropped.Count > 0)
            {
                dbCtx.PlayerPendingNotifications.RemoveRange(dropped);
                await dbCtx.SaveChangesAsync(ct);
            }

            return PlayerNoticeDelivery.Offline;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not keep notice {NoticeKey} for player {PlayerId}",
                notificationType,
                playerId
            );
            return PlayerNoticeDelivery.Failed;
        }
    }

    public async Task DeliverPersistentAsync(PlayerId playerId, CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

            var rows = await dbCtx
                .PlayerPendingNotifications.Where(x => x.PlayerEntityId == playerId.Value)
                .OrderBy(x => x.Id)
                .ToListAsync(ct);

            if (rows.Count == 0)
                return;

            // Deleted before they go out, as offline console messages are marked delivered: a
            // failed delete holds them back for the next login rather than showing them twice.
            dbCtx.PlayerPendingNotifications.RemoveRange(rows);
            await dbCtx.SaveChangesAsync(ct);

            rows = rows.TakeLast(Math.Max(0, config.Value.PersistentNoticeLimit)).ToList();

            foreach (var row in rows)
                await grainFactory.SendComposerToPlayerAsync(
                    playerId,
                    new NotificationDialogMessageComposer
                    {
                        NotificationType = row.NotificationType,
                        Parameters =
                            JsonSerializer
                                .Deserialize<Dictionary<string, string>>(row.Parameters)
                                ?.ToImmutableDictionary()
                            ?? ImmutableDictionary<string, string>.Empty,
                    },
                    ct
                );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not deliver kept notices to player {PlayerId}", playerId);
        }
    }

    private async Task<string> FormatTextAsync(
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    )
    {
        var message = await texts.GetTextAsync(textKey, ct).ConfigureAwait(false) ?? defaultText;
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
        Func<CancellationToken, Task<IComposer>> createComposer,
        CancellationToken ct
    )
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(config.Value.NoticeTimeoutMs);
            var accepted = await grainFactory
                .TrySendComposerToPlayerAsync(
                    playerId,
                    await createComposer(deadline.Token).ConfigureAwait(false),
                    deadline.Token
                )
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
