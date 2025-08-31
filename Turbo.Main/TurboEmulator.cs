using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Core;
using Turbo.Core.Game.Players;
using Turbo.Core.Networking;
using Turbo.DefaultRevision;
using Turbo.Revision20240709;

namespace Turbo.Main;

public class TurboEmulator : IEmulator
{
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
        try
        {
            var networkManager = _serviceProvider.GetRequiredService<INetworkManager>();
            var defaultRevision = ActivatorUtilities.CreateInstance<DefaultRevisionPlugin>(
                _serviceProvider
            );
            var revision20240709 = ActivatorUtilities.CreateInstance<Revision20240709Plugin>(
                _serviceProvider
            );

            await defaultRevision.InitializeAsync();
            await revision20240709.InitializeAsync();
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
        //_logger.LogInformation("{GetVersion} StopAsync...", GetVersion());

        return Task.CompletedTask;
    }

    private void OnStarted()
    {
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
