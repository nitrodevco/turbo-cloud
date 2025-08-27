using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Authorization.Players.Contexts;
using Turbo.Authorization.Players.Requirements;
using Turbo.Core;
using Turbo.Core.Authorization;
using Turbo.Core.Configuration;
using Turbo.Core.Game.Players;
using Turbo.Core.Networking;
using Turbo.DefaultRevision;
using Turbo.Main.Delegates;
using Turbo.Packets.Revisions;

namespace Turbo.Main;

public class TurboEmulator(
    IHostApplicationLifetime appLifetime,
    ILogger<TurboEmulator> logger,
    IServiceProvider serviceProvider,
    TcpSocketHostFactory tcpSocketHostFactory
) : IEmulator
{
    private IHost _tcpSocketHost;
    public const int MAJOR = 0;
    public const int MINOR = 0;
    public const int PATCH = 0;

    /// <summary>
    ///     This method is called by the .NET Generic Host.
    ///     See https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-5.0 for more
    ///     information.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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

        Console.WriteLine("Running {0}", GetVersion());
        Console.WriteLine();

        SetDefaultCulture(CultureInfo.InvariantCulture);

        // Register applicaton lifetime events
        appLifetime.ApplicationStarted.Register(OnStarted);
        appLifetime.ApplicationStopping.Register(OnStopping);
        appLifetime.ApplicationStopped.Register(OnStopped);

        _tcpSocketHost = tcpSocketHostFactory();

        var config = serviceProvider.GetRequiredService<IEmulatorConfig>();

        var defaultRevision = ActivatorUtilities.CreateInstance<DefaultRevisionPlugin>(
            serviceProvider
        );

        await defaultRevision.InitializeAsync();

        await _tcpSocketHost.StartAsync(ct);
    }

    /// <summary>
    ///     This method is called by the .NET Generic Host.
    ///     See https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-5.0 for more
    ///     information.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Shutting down. Disposing services...");
        return Task.CompletedTask;
    }

    public string GetVersion()
    {
        return $"Turbo Emulator {MAJOR}.{MINOR}.{PATCH}";
    }

    /// <summary>
    ///     This method is called by the host application lifetime after the emulator started succesfully.
    /// </summary>
    private void OnStarted()
    {
        logger.LogInformation("Emulator started succesfully!");
    }

    /// <summary>
    ///     This method is called by the host application lifetime right before the emulator starts stopping
    ///     Perform on-stopping activities here.
    ///     This function is called before <see cref="StopAsync(CancellationToken)" />.
    /// </summary>
    private void OnStopping()
    {
        logger.LogInformation("OnStopping has been called.");
    }

    /// <summary>
    ///     This method is called by the host application lifetime after the emulator stopped succesfully.
    /// </summary>
    private void OnStopped()
    {
        logger.LogInformation("{Emulator} shut down gracefully.", GetVersion());
    }

    private void SetDefaultCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
