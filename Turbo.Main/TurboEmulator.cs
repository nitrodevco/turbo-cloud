using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Contracts.Players;
using Turbo.Contracts.Shared;
using Turbo.Core;

namespace Turbo.Main;

public class TurboEmulator(
    IHostApplicationLifetime _appLifetime,
    ILogger<TurboEmulator> _logger,
    IServiceProvider _serviceProvider) : IEmulator
{
    public const int MAJOR = 0;
    public const int MINOR = 0;
    public const int PATCH = 0;

    /// <summary>
    ///     This method is called by the .NET Generic Host.
    ///     See https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-5.0 for more
    ///     information.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine(@"
                ████████╗██╗   ██╗██████╗ ██████╗  ██████╗ 
                ╚══██╔══╝██║   ██║██╔══██╗██╔══██╗██╔═══██╗
                   ██║   ██║   ██║██████╔╝██████╔╝██║   ██║
                   ██║   ██║   ██║██╔══██╗██╔══██╗██║   ██║
                   ██║   ╚██████╔╝██║  ██║██████╔╝╚██████╔╝
                   ╚═╝    ╚═════╝ ╚═╝  ╚═╝╚═════╝  ╚═════╝ 
            ");

        Console.WriteLine("Running {0}", GetVersion());
        Console.WriteLine();

        SetDefaultCulture(CultureInfo.InvariantCulture);

        // Register applicaton lifetime events
        _appLifetime.ApplicationStarted.Register(OnStarted);
        _appLifetime.ApplicationStopping.Register(OnStopping);
        _appLifetime.ApplicationStopped.Register(OnStopped);

        var grainFactory = _serviceProvider.GetRequiredService<IGrainFactory>();

        var ensure = await grainFactory.GetGrain<IPlayerRegistryGrain>("player-1").EnsureExistsAsync(false);

        if (ensure.Status is EnsureStatus.NotFound or EnsureStatus.Failed)
        {
            Console.WriteLine("Failed to ensure player exists: {0}", ensure.Status);
        }
    }

    /// <summary>
    ///     This method is called by the .NET Generic Host.
    ///     See https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-5.0 for more
    ///     information.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down. Disposing services...");
    }

    public string GetVersion()
    {
        return $"Turbo Emulator {MAJOR}.{MINOR}.{PATCH}";
    }

    /// <summary>
    ///     This method is called by the host application lifetime after the emulator started succesfully
    /// </summary>
    private void OnStarted()
    {
        _logger.LogInformation("Emulator started succesfully!");
    }

    /// <summary>
    ///     This method is called by the host application lifetime right before the emulator starts stopping
    ///     Perform on-stopping activities here.
    ///     This function is called before <see cref="StopAsync(CancellationToken)" />
    /// </summary>
    private void OnStopping()
    {
        _logger.LogInformation("OnStopping has been called.");
    }

    /// <summary>
    ///     This method is called by the host application lifetime after the emulator stopped succesfully
    /// </summary>
    private void OnStopped()
    {
        _logger.LogInformation("{Emulator} shut down gracefully.", GetVersion());
    }

    private void SetDefaultCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}