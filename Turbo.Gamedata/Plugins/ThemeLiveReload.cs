using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// Live reload (<see cref="IThemeLiveReload"/>): every change to what the client's variables or
/// texts are made from ends in <see cref="IGamedataFileService.Invalidate"/> - a theme's or a data
/// plugin's files loaded again, staff's variables, texts, who gets a theme - so a moment after one
/// (<see cref="DEBOUNCE_MS"/>; a burst is one) each online player's theme is worked out again and
/// their look (<see cref="IThemeFileService.ThemeLookAsync"/>) compared by revision with the one
/// their client draws: the look it was given before it connected or sent since, else the look every
/// client loads, as of the last time. Only a player whose look differs is sent
/// <see cref="ThemeChanged"/>. This silo's players, for changes this silo made. Not Habbo's.
/// </summary>
internal sealed class ThemeLiveReload(
    IThemeFileService files,
    IThemeResolver resolver,
    IThemeClientState clients,
    IGrainFactory grainFactory,
    IOptions<GamedataConfig> config,
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<ThemeLiveReload> logger,
    ISessionGateway? sessions = null
) : IThemeLiveReload, IHostedService, IDisposable
{
    /// <summary>How long after a change the players are looked at: a save writing several files is one look.</summary>
    public const int DEBOUNCE_MS = 200;

    /// <summary>How long the last look answers <see cref="SettleAsync"/> while no change waits.</summary>
    private static readonly TimeSpan RESULT_KEPT = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _lock = new();
    private Timer? _timer;
    private bool _dirty;
    private ThemeLiveResult? _last;

    /// <summary>The revision of the look a client with no look of its own draws, as of the last look; null before the first.</summary>
    private string? _loaded;

    public Task StartAsync(CancellationToken ct)
    {
        _timer = new Timer(_ => OnTimer(), null, Timeout.Infinite, Timeout.Infinite);
        files.Invalidated += OnInvalidated;

        // The look every client loads, once the plugins are in.
        lifetime.ApplicationStarted.Register(Schedule);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        files.Invalidated -= OnInvalidated;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _gate.Dispose();
    }

    public ThemeLook? Find(string revision) => files.FindLook(revision);

    public async Task<ThemeLiveResult> SettleAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            bool dirty;

            lock (_lock)
            {
                dirty = _dirty;
                _dirty = false;
            }

            if (dirty)
                _last = await LookAsync(ct).ConfigureAwait(false);

            return _last is { At: { } at } last && time.GetUtcNow().UtcDateTime - at <= RESULT_KEPT
                ? last
                : new ThemeLiveResult(0, Online().Count, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>This silo's players online; none on a host with no game connections.</summary>
    private IReadOnlyCollection<PlayerId> Online() => sessions?.GetOnlinePlayerIds() ?? [];

    private void OnInvalidated(string file)
    {
        if (file is GamedataFiles.EXTERNAL_VARIABLES or GamedataFiles.EXTERNAL_TEXTS)
            Schedule();
    }

    private void Schedule()
    {
        lock (_lock)
        {
            _dirty = true;
            _timer?.Change(DEBOUNCE_MS, Timeout.Infinite);
        }
    }

    private void OnTimer() =>
        _ = Task.Run(async () =>
        {
            try
            {
                await SettleAsync(lifetime.ApplicationStopping).ConfigureAwait(false);
            }
            catch (Exception ex) when (!lifetime.ApplicationStopping.IsCancellationRequested)
            {
                logger.LogError(ex, "Sending players the hotel's new look failed");
            }
        });

    /// <summary>
    /// Works out every online player's look and sends each whose client draws another one the new;
    /// the look a client with no look of its own draws becomes the default's.
    /// </summary>
    private async Task<ThemeLiveResult> LookAsync(CancellationToken ct)
    {
        var online = Online();
        var at = time.GetUtcNow().UtcDateTime;

        var requests = online.ToDictionary(x => x, x => clients.DrawnBy(x)?.Requested);
        var (theme, players) = await resolver.ResolveAsync(requests, ct).ConfigureAwait(false);
        var looks = new Dictionary<string, ThemeLook>(StringComparer.OrdinalIgnoreCase);

        async Task<ThemeLook> LookOfAsync(string? key)
        {
            if (!looks.TryGetValue(key ?? string.Empty, out var look))
                looks[key ?? string.Empty] = look = await files
                    .ThemeLookAsync(key, ct)
                    .ConfigureAwait(false);

            return look;
        }

        var loaded = (await LookOfAsync(theme).ConfigureAwait(false)).Revision;
        var addresses = await ExternalVariablesFile
            .AddressesAsync(files, config.Value.PublicUrl, ct)
            .ConfigureAwait(false);
        var sent = 0;

        foreach (var (player, drawn) in players)
        {
            var look = await LookOfAsync(drawn).ConfigureAwait(false);
            var own = clients.DrawnBy(player);

            // Before the first look nothing says what a client with no look of its own draws.
            if ((own?.Revision ?? _loaded) is not { } had || had == look.Revision)
                continue;

            clients.NoteDrawn(player, own?.Requested, look.Revision);
            sent++;
            grainFactory
                .SendPluginMessageToPlayerAsync(
                    player,
                    new ThemeChanged(
                        look.Theme,
                        look.Revision,
                        $"{addresses.Root}{GamedataServer.THEME_LIVE_ROUTE}{look.Revision}"
                    ),
                    CancellationToken.None
                )
                .LogAndForget(logger, "send player {PlayerId} the hotel's new look", player);
        }

        _loaded = loaded;
        clients.ForgetDrawn(online.ToHashSet());

        if (sent > 0)
            logger.LogInformation(
                "The hotel's look changed: sent to {Players} of {Online} players online",
                sent,
                online.Count
            );

        return new ThemeLiveResult(sent, online.Count, at);
    }
}
