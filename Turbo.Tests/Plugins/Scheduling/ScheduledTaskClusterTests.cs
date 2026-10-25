using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;
using Turbo.Contracts.Plugins.Scheduling;
using Turbo.Database.Context;
using Turbo.Operations.Scheduling;
using Turbo.Primitives.Orleans;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins.Scheduling;

/// <summary>
/// Two servers in one cluster, both with the same plugin's task every second: each tick runs it
/// once, on one of them, never on both (docs/plugin-scheduler.md "One run per cluster"). Real
/// Orleans silos in this process, as a hotel runs them, so the guarantee is the grain's.
/// </summary>
public sealed class ScheduledTaskClusterTests : IAsyncDisposable
{
    private const string KEY = "rates.Reprice";

    private readonly SqliteDb _db = new();
    private readonly List<IHost> _silos = [];
    private readonly ConcurrentQueue<(int Silo, DateTimeOffset ScheduledFor)> _runs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        foreach (var silo in Enumerable.Reverse(_silos))
        {
            await silo.StopAsync(CancellationToken.None);
            silo.Dispose();
        }

        _db.Dispose();
    }

    [Fact]
    public async Task Two_servers_with_the_same_task_run_each_tick_once()
    {
        var cluster = "sched-" + Guid.NewGuid().ToString("N")[..8];
        var primary = new IPEndPoint(IPAddress.Loopback, TestPorts.Free());

        await StartSiloAsync(1, primary.Port, null, cluster);
        await StartSiloAsync(2, TestPorts.Free(), primary, cluster);

        var grains = _silos[0].Services.GetRequiredService<IGrainFactory>();

        // Both have it loaded, so either could run any tick.
        await WaitForAsync(async () =>
            (await grains.GetScheduledTaskGrain().ListAsync(Ct)) is [{ Servers: 2 }]
        );

        var from = _runs.Count;

        await WaitForAsync(() => Task.FromResult(_runs.Count >= from + 4));

        var runs = _runs.ToArray();

        runs.Select(r => r.ScheduledFor)
            .Should()
            .OnlyHaveUniqueItems("a tick run on both servers would show twice");
        runs.Select(r => r.ScheduledFor).Should().BeInAscendingOrder();
    }

    private async Task StartSiloAsync(int number, int siloPort, IPEndPoint? primary, string cluster)
    {
        var silo = new HostBuilder()
            .UseOrleans(builder =>
                builder.UseLocalhostClustering(
                    siloPort,
                    TestPorts.Free(),
                    primary,
                    serviceId: cluster,
                    clusterId: cluster
                )
            )
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services.AddSingleton<IDbContextFactory<TurboDbContext>>(_db);
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<ScheduledTaskHost>();
                services.AddHostedService(sp => sp.GetRequiredService<ScheduledTaskHost>());
            })
            .Build();

        await silo.StartAsync(Ct);
        _silos.Add(silo);

        silo.Services.GetRequiredService<ScheduledTaskHost>()
            .Register(
                "rates",
                [
                    new ScheduledTaskDefinition(
                        KEY,
                        new Reprice(number, _runs),
                        new EveryAttribute("00:00:01")
                    ),
                ]
            );
    }

    private static async Task WaitForAsync(Func<Task<bool>> done)
    {
        // Silos join and the grain ticks in real time: up to 30 s on a loaded machine.
        for (var i = 0; i < 300 && !await done(); i++)
            await Task.Delay(100, Ct);

        (await done()).Should().BeTrue();
    }

    private sealed class Reprice(int silo, ConcurrentQueue<(int, DateTimeOffset)> runs)
        : IScheduledTask
    {
        public Task RunAsync(ScheduledTaskRun run, CancellationToken ct)
        {
            runs.Enqueue((silo, run.ScheduledFor));

            return Task.CompletedTask;
        }
    }
}
