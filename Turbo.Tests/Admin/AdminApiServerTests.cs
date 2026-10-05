using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Catalog;
using Turbo.Admin.Commands;
using Turbo.Admin.Configuration;
using Turbo.Admin.Links;
using Turbo.Admin.Live;
using Turbo.Admin.Permissions;
using Turbo.Admin.Players;
using Turbo.Admin.Rooms;
using Turbo.Database.Context;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The admin API is a convenience beside the hotel: when it cannot have its address, the hotel
/// starts without it rather than failing to start at all; and a browser leaving mid-request is
/// nothing to report.
/// </summary>
public sealed class AdminApiServerTests : IDisposable
{
    private readonly TcpListener _taken = new(IPAddress.Loopback, 0);

    public AdminApiServerTests() => _taken.Start();

    public void Dispose() => _taken.Dispose();

    [Fact]
    public async Task APortAlreadyInUse_LeavesTheHotelRunning_AndSaysWhy()
    {
        var port = ((IPEndPoint)_taken.LocalEndpoint).Port;
        var (server, log, _) = Build(
            $"http://127.0.0.1:{port}",
            new Fakes(),
            NullLoggerFactory.Instance
        );

        // Before, the bind failure came out of StartAsync and stopped the whole host.
        var start = () => server.StartAsync(TestContext.Current.CancellationToken);

        await start.Should().NotThrowAsync();
        log.Where(x => x.Level >= LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain($"{port}");

        await server.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ABrowserLeavingMidRequest_IsNotAFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var port = ((IPEndPoint)_taken.LocalEndpoint).Port;

        _taken.Stop(); // The port, free for the server.

        // The session check waits on its grain until the request gives up.
        var fakes = new Fakes();
        fakes.Handlers["GetSessionAsync"] = call =>
            Task.Delay(Timeout.Infinite, (CancellationToken)call.Args[1]!)
                .ContinueWith<AdminSessionSnapshot?>(
                    _ => null,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default
                );
        var hostLog = new CapturingLoggerFactory();
        var (server, log, _) = Build($"http://127.0.0.1:{port}", fakes, hostLog);

        await server.StartAsync(ct);

        try
        {
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"http://127.0.0.1:{port}/api/me"
            );
            using var gone = CancellationTokenSource.CreateLinkedTokenSource(ct);

            request.Headers.Authorization = new("Bearer", "some-session");
            gone.CancelAfter(TimeSpan.FromMilliseconds(300));

            var send = () => client.SendAsync(request, gone.Token);

            await send.Should().ThrowAsync<OperationCanceledException>();

            // The server notices the dropped connection on its own time.
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        finally
        {
            await server.StopAsync(ct);
        }

        // Not a failure of the panel's: nothing logged as one. (The cancellation is also caught
        // in the panel's own code now, so a debugger no longer stops on it as unhandled; that
        // part shows only in a debugger, not here.)
        hostLog
            .Entries.Concat(log)
            .Where(x => x.Level >= LogLevel.Warning || x.Exception is OperationCanceledException)
            .Should()
            .BeEmpty();
    }

    /// <summary>
    /// The server as the host builds it, over fakes of the hotel it reaches, and the services it
    /// was built from (the live feed among them). <paramref name="options"/>, when given, is the
    /// whole config, its address included.
    /// </summary>
    internal static (
        IHostedService Server,
        ConcurrentQueue<LogEntry> Log,
        IServiceProvider Services
    ) Build(string url, Fakes fakes, ILoggerFactory hostLog, AdminConfig? options = null)
    {
        var config = Options.Create(options ?? new AdminConfig { Enabled = true, Url = url });
        var services = new ServiceCollection();

        foreach (
            var iface in new[]
            {
                typeof(IGrainFactory),
                typeof(ICommandRegistryProvider),
                typeof(IOperatorCommandRunner),
                typeof(IHotelTextProvider),
                typeof(ISessionGateway),
                typeof(IHotelAvailability),
                typeof(IPermissionEditService),
                typeof(IPermissionRegistryProvider),
                typeof(IPlayerNoticeService),
                typeof(IDbContextFactory<TurboDbContext>),
                typeof(IRoomService),
                typeof(IFurnitureDefinitionProvider),
                typeof(ICatalogEditService),
                typeof(IPlayerAccountService),
                typeof(ILoginTicketService),
            }
        )
            services.AddSingleton(iface, fakes.Create(iface));

        services.AddSingleton(config);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<AdminLinkPolicy>();
        services.AddSingleton<AdminRoomQueries>();
        services.AddSingleton<AdminRoomEditor>();
        services.AddSingleton<AdminPlayerQueries>();
        services.AddSingleton<AdminTicketPolicy>();
        services.AddSingleton<AdminSiteAccounts>();
        services.AddSingleton<AdminCommandLogQueries>();
        services.AddSingleton<AdminCatalogQueries>();
        services.AddSingleton<PermissionViews>();
        services.AddSingleton<AdminLiveFeed>();

        // Internal to Turbo.Admin, as the host knows it.
        var serverType = typeof(AdminConfig).Assembly.GetType("Turbo.Admin.Api.AdminApiServer")!;
        var logger = Activator.CreateInstance(
            typeof(CapturingLogger<>).MakeGenericType(serverType)
        )!;
        var provider = services.BuildServiceProvider();
        var server = (IHostedService)
            Activator.CreateInstance(serverType, provider, config, hostLog, logger)!;

        return (
            server,
            (ConcurrentQueue<LogEntry>)logger.GetType().GetProperty("Entries")!.GetValue(logger)!,
            provider
        );
    }
}

/// <summary>Every log entry the web host writes, Kestrel's included, at any level.</summary>
internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(Entries);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    private sealed class Logger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}
