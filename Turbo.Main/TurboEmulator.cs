using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.DefaultRevision;
using Turbo.Events.Abstractions;
using Turbo.Networking.Abstractions;
using Turbo.Primitives.Events;
using Turbo.Revision20240709;

namespace Turbo.Main;

public class TurboEmulator : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<TurboEmulator> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly List<IDisposable> _registrations;
    private readonly INetworkManager _networkManager;

    public TurboEmulator(
        IHostApplicationLifetime appLifetime,
        ILogger<TurboEmulator> logger,
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        INetworkManager networkManager
    )
    {
        _appLifetime = appLifetime;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
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
            var defaultRevision = ActivatorUtilities.CreateInstance<DefaultRevisionPlugin>(
                _serviceProvider
            );
            var revision20240709 = ActivatorUtilities.CreateInstance<Revision20240709Plugin>(
                _serviceProvider
            );

            await defaultRevision.InitializeAsync();
            await revision20240709.InitializeAsync();
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
        _eventBus.PublishAsync(new PlayerJoinedEvent(1));
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
