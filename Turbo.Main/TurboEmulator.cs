using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using SuperSocket.Connection;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;
using Turbo.Authorization.Players.Contexts;
using Turbo.Authorization.Players.Requirements;
using Turbo.Core;
using Turbo.Core.Authorization;
using Turbo.Core.Configuration;
using Turbo.Core.Game.Players;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Protocol;
using Turbo.DefaultRevision;
using Turbo.Main.Delegates;
using Turbo.Packets.Revisions;

namespace Turbo.Main;

public class TurboEmulator : IEmulator
{
    public const int MAJOR = 0;
    public const int MINOR = 0;
    public const int PATCH = 0;

    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<TurboEmulator> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IDisposable> _registrations;

    public TurboEmulator(
        IHostApplicationLifetime appLifetime,
        ILogger<TurboEmulator> logger,
        IServiceProvider serviceProvider
    )
    {
        _appLifetime = appLifetime;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _registrations =
        [
            _appLifetime.ApplicationStarted.Register(OnStarted),
            _appLifetime.ApplicationStopping.Register(OnStopping),
            _appLifetime.ApplicationStopped.Register(OnStopped),
        ];

        SetDefaultCulture(CultureInfo.InvariantCulture);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine(
            @"
                ████████╗██╗   ██╗██████╗ ██████╗  ██████╗ 
                ╚══██╔══╝██║   ██║██╔══██╗██╔══██╗██╔═══██╗
                   ██║   ██║   ██║██████╔╝██████╔╝██║   ██║
                   ██║   ██║   ██║██╔══██╗██╔══██╗██║   ██║
                   ██║   ╚██████╔╝██║  ██║██████╔╝╚██████╔╝
                   ╚═╝    ╚═════╝ ╚═╝  ╚═╝╚═════╝  ╚═════╝ 
            "
        );

        _logger.LogInformation("Starting {Emulator}", GetVersion());
        Console.WriteLine();

        try
        {
            var networkManager = _serviceProvider.GetRequiredService<INetworkManager>();
            var defaultRevision = ActivatorUtilities.CreateInstance<DefaultRevisionPlugin>(
                _serviceProvider
            );

            await defaultRevision.InitializeAsync();
            await networkManager.StartAsync();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Emulator startup was cancelled.");

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Emulator failed to start!");

            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{GetVersion} StopAsync...", GetVersion());

        return Task.CompletedTask;
    }

    private void OnStarted()
    {
        _logger.LogInformation("Started {Emulator}", GetVersion());
        Console.WriteLine();
    }

    private void OnStopping()
    {
        _logger.LogInformation("{GetVersion} Stopping...", GetVersion());
    }

    private void OnStopped()
    {
        _logger.LogInformation("{GetVersion} Stopped", GetVersion());
    }

    public string GetVersion()
    {
        return $"Turbo Emulator {MAJOR}.{MINOR}.{PATCH}";
    }

    private void SetDefaultCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
