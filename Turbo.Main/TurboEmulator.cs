using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Networking.Abstractions;

namespace Turbo.Main;

public class TurboEmulator : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<TurboEmulator> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IDisposable> _registrations;
    private readonly INetworkManager _networkManager;

    public TurboEmulator(
        IHostApplicationLifetime appLifetime,
        ILogger<TurboEmulator> logger,
        IServiceProvider serviceProvider,
        INetworkManager networkManager
    )
    {
        _appLifetime = appLifetime;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _networkManager = networkManager;
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
        try
        {
            List<Assembly> allLoaded = [];
            // var allLoaded = AssemblyScanUtil.GetLoadedAssemblies();

            foreach (var asm in allLoaded)
            {
                /* _eventBus.RegisterFromAssembly(
                    "Turbo",
                    asm,
                    _serviceProvider,
                    useAmbientScope: true
                );

                _messageBus.RegisterFromAssembly(
                    "Turbo",
                    asm,
                    _serviceProvider,
                    useAmbientScope: true
                ); */
            }

            await _networkManager.StartAsync();
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
        //_logger.LogInformation("{GetVersion} StopAsync...", GetVersion());

        return Task.CompletedTask;
    }

    private void OnStarted()
    {
        //_eventBus.PublishAsync(new PlayerJoinedEvent(1));
        //_logger.LogInformation("Started {Emulator}", GetVersion());
    }

    private void OnStopping()
    {
        //_logger.LogInformation("{GetVersion} Stopping...", GetVersion());
    }

    private void OnStopped()
    {
        //_logger.LogInformation("{GetVersion} Stopped", GetVersion());
    }

    private void SetDefaultCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
