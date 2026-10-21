using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Texts;

namespace Turbo.Operations;

/// <summary>
/// Follows the hotel's countdown (<see cref="IHotelAvailability"/>) and keeps the notice players
/// see in step with it: put up when a countdown of a kind the hotel turned on reaches its first
/// <see cref="CountdownNoticeConfig.PostAtMinutes"/> (or at once), again at each later one, taken
/// down when it is called off, runs out or its kind is turned off. It never changes what Habbo's
/// own closing alerts say or when they come. It is process state, as the countdown is: a restart
/// takes it down with everything else.
/// </summary>
public sealed class HotelCountdownNoticeService(
    IOptionsMonitor<OperationsConfig> config,
    IHotelAvailability availability,
    TimeProvider timeProvider,
    ISessionGateway sessionGateway,
    IGrainFactory grainFactory,
    IHotelTextProvider textProvider,
    ILogger<IHotelCountdownNotice> logger
) : BackgroundService, IHotelCountdownNotice
{
    public const string TEXT_PREFIX = "hotel.countdown_notice.";

    /// <summary>The countdown notice layout (<c>new_feature_notification_countdown</c>).</summary>
    private const string TYPE_COUNTDOWN = "countdown";

    /// <summary>The hotel time a notice counts down to, as <c>GetSecondsUntil</c> reads it.</summary>
    private const string TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";

    private const string CLOSE_NEVER = "never";

    private static readonly IReadOnlyDictionary<HotelCountdownKind, string> DEFAULT_TEXTS =
        new Dictionary<HotelCountdownKind, string>
        {
            [HotelCountdownKind.Restart] = "Hotel restart in",
            [HotelCountdownKind.Shutdown] = "Hotel closing in",
            [HotelCountdownKind.Maintenance] = "Maintenance break in",
        };

    private readonly Lock _gate = new();
    private readonly HashSet<int> _posted = [];

    private HotelAvailabilitySnapshot? _countdown;
    private TurboNoticeMessage? _current;

    public TurboNoticeMessage? Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public bool IsEnabled(HotelCountdownKind kind) => SettingsOf(kind).Enabled;

    public string DefaultLink(HotelCountdownKind kind) => SettingsOf(kind).DefaultLink.Trim();

    /// <summary>The hotel text a notice of this kind reads.</summary>
    public static string TextKey(HotelCountdownKind kind) =>
        TEXT_PREFIX + kind.ToString().ToLowerInvariant();

    /// <summary>The key a notice of this kind is shown under.</summary>
    public static string NoticeKey(HotelCountdownKind kind) =>
        "countdown_" + kind.ToString().ToLowerInvariant();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(config.CurrentValue.CountdownTickMs)
        );

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await SyncAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping, and the notice with it.
        }
    }

    /// <summary>One look at the countdown: put the notice up, again, or take it down.</summary>
    public async Task SyncAsync(CancellationToken ct)
    {
        var countdown = availability.Current;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var kind = KindOf(countdown);
        var settings = kind is { } k ? SettingsOf(k) : null;
        TurboNoticeMessage? takeDown = null;
        var post = false;
        var remaining = TimeSpan.Zero;

        lock (_gate)
        {
            if (
                settings is not { Enabled: true }
                || countdown.AtUtc is not { } at
                || (remaining = at - now) <= TimeSpan.Zero
            )
            {
                takeDown = _current;
                _current = null;
                _countdown = null;
                _posted.Clear();
            }
            else
            {
                // Another countdown, or this one started again: what was up is for the old one.
                if (!ReferenceEquals(_countdown, countdown))
                {
                    if (_current is not null && _current.Key != NoticeKey(kind!.Value))
                        takeDown = _current;

                    _current = null;
                    _countdown = countdown;
                    _posted.Clear();
                }

                if (settings.PostAtMinutes.Length == 0)
                    post = _posted.Add(int.MaxValue);
                else
                    // A point the countdown started inside is reached at once, with the others it is in.
                    foreach (var minutes in settings.PostAtMinutes)
                        if (remaining <= TimeSpan.FromMinutes(minutes) && _posted.Add(minutes))
                            post = true;
            }
        }

        if (takeDown is not null)
            await SendAsync(takeDown with { Shown = false }, ct);

        if (!post)
            return;

        var notice = await BuildAsync(kind!.Value, settings!, countdown, remaining, ct);

        lock (_gate)
        {
            // Called off or replaced while its text was read.
            if (!ReferenceEquals(_countdown, countdown))
                return;

            _current = notice;
        }

        await SendAsync(notice, ct);
    }

    private static HotelCountdownKind? KindOf(HotelAvailabilitySnapshot countdown) =>
        countdown.Phase switch
        {
            HotelAvailabilityPhase.ShutdownScheduled when countdown.Restart =>
                HotelCountdownKind.Restart,
            HotelAvailabilityPhase.ShutdownScheduled => HotelCountdownKind.Shutdown,
            HotelAvailabilityPhase.MaintenanceScheduled => HotelCountdownKind.Maintenance,
            _ => null,
        };

    private CountdownNoticeConfig SettingsOf(HotelCountdownKind kind) =>
        kind switch
        {
            HotelCountdownKind.Restart => config.CurrentValue.RestartNotice,
            HotelCountdownKind.Shutdown => config.CurrentValue.ShutdownNotice,
            _ => config.CurrentValue.MaintenanceNotice,
        };

    private async Task<TurboNoticeMessage> BuildAsync(
        HotelCountdownKind kind,
        CountdownNoticeConfig settings,
        HotelAvailabilitySnapshot countdown,
        TimeSpan remaining,
        CancellationToken ct
    )
    {
        var at = countdown.AtUtc!.Value;
        var time = at.ToString(TIME_FORMAT, CultureInfo.InvariantCulture);
        var variables = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        variables["type"] = TYPE_COUNTDOWN;
        variables["count_down_to"] = time;
        // A client started after the end drops it unseen, as it does any expired notice.
        variables["expiry"] = time;

        var link =
            countdown.NoticeLink.Trim().Length > 0 ? countdown.NoticeLink : settings.DefaultLink;

        if (CountdownNoticeLink.TryRead(link, out var linkVariable, out var linkValue))
            variables[linkVariable] = linkValue;
        else if (link.Trim().Length > 0)
            logger.LogWarning(
                "The {Kind} notice's link is neither a web address nor a client link; it is shown without one",
                kind
            );

        if (settings.Image.Trim().Length > 0)
            variables["image"] = settings.Image.Trim();

        if (settings.Color.Trim().Length > 0)
            variables["color"] = settings.Color.Trim();

        if (!settings.Dismissible)
            variables["close"] = CLOSE_NEVER;

        var template =
            await textProvider.GetTextAsync(TextKey(kind), ct).ConfigureAwait(false)
            ?? DEFAULT_TEXTS[kind];

        return new TurboNoticeMessage
        {
            Key = NoticeKey(kind),
            Shown = true,
            Variables = variables.ToImmutable(),
            Description = template
                .Replace(
                    "{minutes}",
                    ((int)Math.Ceiling(remaining.TotalMinutes)).ToString(
                        CultureInfo.InvariantCulture
                    ),
                    StringComparison.Ordinal
                )
                .Replace(
                    "{time}",
                    at.ToString("HH:mm", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal
                )
                .Replace("{reason}", countdown.Reason, StringComparison.Ordinal),
        };
    }

    private async Task SendAsync(TurboNoticeMessage notice, CancellationToken ct)
    {
        try
        {
            await grainFactory.SendComposerToPlayersAsync(
                sessionGateway.GetOnlinePlayerIds(),
                notice,
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to send the countdown notice {Key} (shown {Shown})",
                notice.Key,
                notice.Shown
            );
        }
    }
}
