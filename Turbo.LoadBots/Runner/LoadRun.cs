using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.LoadBots.Behaviour;
using Turbo.LoadBots.Client;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Metrics;
using Turbo.LoadBots.Provisioning;

namespace Turbo.LoadBots.Runner;

/// <summary>
/// The load test: brings the bots online at the ramp rate, lets them live their lives for the
/// configured time, prints progress as it goes and writes the report at the end.
/// </summary>
public sealed class LoadRun(
    LoadBotOptions options,
    HotelKnowledge knowledge,
    ILoggerFactory loggerFactory
)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<LoadRun>();

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var run = options.Run;
        var accounts = await AccountsJson.ReadAsync(options.AccountsFile, ct);

        if (run.Bots > 0)
            accounts = [.. accounts.Take(run.Bots)];

        if (accounts.Count == 0)
        {
            _logger.LogError("No bot accounts to run; provision some first.");

            return 1;
        }

        var metrics = new BotMetrics();
        var world = new SharedWorld([.. knowledge.WiredOffers.Keys]);
        var personas = AssignPersonas(accounts.Count, run.Personas, new Random(run.Seed));
        var agents = accounts
            .Select((account, i) => CreateAgent(account, personas[i], run.Seed + i, metrics, world))
            .ToList();

        _logger.LogInformation(
            "Starting {Count} bots ({Personas}) for {Minutes} minutes after ramp-up",
            agents.Count,
            string.Join(", ", ReportWriter.CountPersonas(agents).Select(x => $"{x.Value} {x.Key}")),
            run.DurationMinutes
        );

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var rampSeconds = agents.Count / Math.Max(0.1, run.RampPerSecond);
        stop.CancelAfter(
            TimeSpan.FromSeconds(rampSeconds) + TimeSpan.FromMinutes(run.DurationMinutes)
        );

        var tasks = new List<Task>(agents.Count);
        var progress = ReportProgressAsync(metrics, agents, stop.Token);

        try
        {
            for (var i = 0; i < agents.Count && !stop.IsCancellationRequested; i++)
            {
                tasks.Add(agents[i].RunAsync(stop.Token));
                await Task.Delay(
                    TimeSpan.FromSeconds(1 / Math.Max(0.1, run.RampPerSecond)),
                    stop.Token
                );
            }

            await Task.Delay(Timeout.Infinite, stop.Token);
        }
        catch (OperationCanceledException)
        {
            // The run's time is up, or the operator pressed Ctrl+C.
        }

        // A bot that crashed is a failure to report, not a reason to lose everyone's results.
        foreach (var (task, agent) in tasks.Zip(agents))
        {
            try
            {
                await task;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                metrics.Check(
                    "bot.runs_to_the_end",
                    false,
                    CheckSeverity.Hard,
                    $"{agent.Context.Client.Name}: {ex.GetType().Name}: {ex.Message}"
                );
            }
        }

        await progress;

        var report = new RunReport
        {
            Mode = "run",
            Bots = agents.Count,
            Personas = ReportWriter.CountPersonas(agents),
            Metrics = metrics.Report(),
            WiredOutcomes = world.WiredOutcomes(),
            WiredNotTried = world.UntriedWiredLogics(),
        };

        Console.WriteLine(ReportWriter.Summary(report));
        Console.WriteLine(
            $"Report: {await ReportWriter.WriteAsync(run.ReportFile, report, CancellationToken.None)}"
        );

        return 0;
    }

    private BotAgent CreateAgent(
        BotAccount account,
        Persona persona,
        int seed,
        BotMetrics metrics,
        SharedWorld world
    )
    {
        var logger = loggerFactory.CreateLogger($"Bot.{account.Name}");
        var client = new BotClient(account, options, knowledge, metrics, logger);

        return new BotAgent(
            new BotContext(
                client,
                persona,
                new Random(seed),
                options,
                knowledge,
                metrics,
                world,
                logger
            )
        );
    }

    /// <summary>
    /// Personas in proportion to their weights, shuffled with the run's seed so the same seed
    /// gives each account the same persona every time.
    /// </summary>
    public static List<Persona> AssignPersonas(
        int count,
        IReadOnlyDictionary<string, double> weights,
        Random random
    )
    {
        var parsed = weights
            .Where(x => x.Value > 0)
            .Select(x =>
                Enum.TryParse<Persona>(x.Key, ignoreCase: true, out var persona)
                    ? (Persona: persona, Weight: x.Value)
                    : throw new ArgumentException(
                        $"Unknown persona '{x.Key}' in LoadBots:Run:Personas."
                    )
            )
            .OrderBy(x => x.Persona)
            .ToList();

        if (parsed.Count == 0)
            throw new ArgumentException(
                "LoadBots:Run:Personas gives no persona a positive weight."
            );

        var total = parsed.Sum(x => x.Weight);
        var personas = new List<Persona>(count);

        // Largest remainder: each persona's exact share, rounded so the counts add up.
        var shares = parsed
            .Select(x => (x.Persona, Exact: x.Weight / total * count))
            .Select(x =>
                (
                    x.Persona,
                    Whole: (int)Math.Floor(x.Exact),
                    Remainder: x.Exact - Math.Floor(x.Exact)
                )
            )
            .ToList();

        foreach (var (persona, whole, _) in shares)
            personas.AddRange(Enumerable.Repeat(persona, whole));

        foreach (
            var (persona, _, _) in shares
                .OrderByDescending(x => x.Remainder)
                .Take(count - personas.Count)
        )
            personas.Add(persona);

        return [.. personas.OrderBy(_ => random.Next())];
    }

    private async Task ReportProgressAsync(
        BotMetrics metrics,
        List<BotAgent> agents,
        CancellationToken ct
    )
    {
        var meter = new ProgressMeter(metrics);
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Run.ReportIntervalSeconds));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(interval, ct);

                var online = agents.Count(x => x.Context.Client.IsConnected);

                Console.WriteLine(meter.Line(online, agents.Count));
            }
        }
        catch (OperationCanceledException)
        {
            // The run is over.
        }
    }
}
