using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.LoadBots.Behaviour;
using Turbo.LoadBots.Behaviour.Activities;
using Turbo.LoadBots.Client;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Metrics;
using Turbo.LoadBots.Provisioning;

namespace Turbo.LoadBots.Runner;

/// <summary>
/// One deliberate pass over everything the bots can do, by a handful of bots, ending with a
/// pass or fail: an architect builds and redraws a room and a wired machine, two engineers
/// save every wired box the catalog sells, a host builds a game and runs a round while a
/// visitor tours the architect's room and plays. Exits non-zero on any hard failure, so it can
/// gate a deploy against a running server.
/// </summary>
public sealed class SmokeRun(
    LoadBotOptions options,
    HotelKnowledge knowledge,
    ILoggerFactory loggerFactory
)
{
    private const int BOTS_NEEDED = 5;

    private readonly ILogger _logger = loggerFactory.CreateLogger<SmokeRun>();

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var accounts = await AccountsJson.ReadAsync(options.AccountsFile, ct);

        if (accounts.Count < BOTS_NEEDED)
        {
            _logger.LogError(
                "The smoke run needs {Needed} bot accounts; provision more.",
                BOTS_NEEDED
            );

            return 1;
        }

        var metrics = new BotMetrics();
        var world = new SharedWorld([.. knowledge.WiredOffers.Keys]);
        var seed = options.Run.Seed;

        var architect = Create(accounts[0], Persona.Architect, seed, metrics, world);
        var engineers = new[]
        {
            Create(accounts[1], Persona.WiredEngineer, seed + 1, metrics, world),
            Create(accounts[2], Persona.WiredEngineer, seed + 2, metrics, world),
        };
        var host = Create(accounts[3], Persona.GameHost, seed + 3, metrics, world);
        var visitor = Create(accounts[4], Persona.Visitor, seed + 4, metrics, world);
        var everyone = new[] { architect, engineers[0], engineers[1], host, visitor };

        var loggedIn = await Task.WhenAll(everyone.Select(x => LogInAsync(x, ct)));

        if (loggedIn.Contains(false))
        {
            _logger.LogError("Not every smoke bot could log in; is the server up and provisioned?");
            await Task.WhenAll(everyone.Select(x => x.Client.DisconnectAsync()));

            return 1;
        }

        var gameReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var roomReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        await Task.WhenAll(
            StepAsync("architect", ArchitectAsync(architect, roomReady, ct)),
            StepAsync("engineer 1", EngineerAsync(engineers[0], ct)),
            StepAsync("engineer 2", EngineerAsync(engineers[1], ct)),
            StepAsync("host", HostAsync(host, gameReady, ct)),
            StepAsync("visitor", VisitorAsync(visitor, roomReady.Task, gameReady.Task, ct))
        );

        await Task.WhenAll(everyone.Select(x => x.Client.DisconnectAsync()));

        // Every box the catalog sells should have been tried by one engineer or the other.
        metrics.Check(
            "wired.every_box_type_tried",
            world.UntriedWiredLogics().Count == 0,
            CheckSeverity.Hard,
            $"not reached: {string.Join(", ", world.UntriedWiredLogics())}"
        );

        var report = new RunReport
        {
            Mode = "smoke",
            Bots = everyone.Length,
            Personas = everyone
                .GroupBy(x => x.Persona.ToString())
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal),
            Metrics = metrics.Report(),
            WiredOutcomes = world.WiredOutcomes(),
            WiredNotTried = world.UntriedWiredLogics(),
        };

        Console.WriteLine(ReportWriter.Summary(report));
        Console.WriteLine(
            $"Report: {await ReportWriter.WriteAsync(options.Run.ReportFile, report, CancellationToken.None)}"
        );

        var passed = !metrics.AnyHardFailure;

        Console.WriteLine(passed ? "SMOKE PASSED" : "SMOKE FAILED");

        return passed ? 0 : 1;

        // A step that throws is a failed check, not a crashed run.
        async Task StepAsync(string name, Task step)
        {
            try
            {
                await step;
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                metrics.Check(
                    "smoke.step_completes",
                    false,
                    CheckSeverity.Hard,
                    $"{name}: {ex.GetType().Name}: {ex.Message}"
                );
                _logger.LogError(ex, "Smoke step {Step} failed", name);
            }
        }
    }

    private static async Task ArchitectAsync(
        BotContext ctx,
        TaskCompletionSource roomReady,
        CancellationToken ct
    )
    {
        try
        {
            await BuildActivities.DecorateAsync(ctx, 4, 6, ct);
            await BuildActivities.RearrangeAsync(ctx, ct);
        }
        finally
        {
            roomReady.TrySetResult();
        }

        await BuildActivities.DrawFloorPlanAsync(ctx, ct);
        await WiredActivities.BuildContraptionAsync(ctx, ct);
    }

    private static async Task EngineerAsync(BotContext ctx, CancellationToken ct)
    {
        // The two engineers share the rotation; each lab pass takes a few boxes.
        while (!ct.IsCancellationRequested && ctx.World.UntriedWiredLogics().Count > 0)
        {
            if (!ctx.Client.IsConnected)
                return;

            await WiredActivities.WiredLabAsync(ctx, ct);
        }
    }

    private static async Task HostAsync(
        BotContext ctx,
        TaskCompletionSource gameReady,
        CancellationToken ct
    )
    {
        if (
            await ctx.OwnRoomAsync(RoomPurpose.Game, ct) is not { } roomId
            || !await ctx.EnterAsync(roomId, ct)
        )
        {
            gameReady.TrySetResult();

            return;
        }

        // The first call builds the game; give the visitor a head start into the room, then run
        // a round with the visitor on the pads.
        await GameActivities.HostAsync(ctx, ct);
        gameReady.TrySetResult();

        ctx.Metrics.Check(
            "game.build.published",
            ctx.World.Game(roomId) is not null,
            CheckSeverity.Hard,
            $"{ctx.Client.Name}: room {roomId}"
        );

        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        await GameActivities.HostAsync(ctx, ct);
    }

    private static async Task VisitorAsync(
        BotContext ctx,
        Task roomReady,
        Task gameReady,
        CancellationToken ct
    )
    {
        await roomReady.WaitAsync(ct);
        await SocialActivities.VisitAsync(ctx, ct);

        await gameReady.WaitAsync(ct);
        await GameActivities.PlayAsync(ctx, ct);
        await GameActivities.PlayAsync(ctx, ct);
    }

    private static async Task<bool> LogInAsync(BotContext ctx, CancellationToken ct)
    {
        if (!await ctx.Client.ConnectAsync(ct))
            return false;

        await ctx.Client.LoadInventoryAsync(ct);
        await ctx.Client.LoadCatalogIndexAsync(ct);

        return true;
    }

    private BotContext Create(
        BotAccount account,
        Persona persona,
        int seed,
        BotMetrics metrics,
        SharedWorld world
    )
    {
        var logger = loggerFactory.CreateLogger($"Bot.{account.Name}");

        return new BotContext(
            new BotClient(account, options, knowledge, metrics, logger),
            persona,
            new Random(seed),
            options,
            knowledge,
            metrics,
            world,
            logger
        );
    }
}
