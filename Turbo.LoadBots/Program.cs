using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.LoadBots;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Provisioning;
using Turbo.LoadBots.Runner;

const string USAGE = """
    Turbo load-test bots

    Usage: dotnet run --project Turbo.LoadBots -- <command> [--LoadBots:<Option>=<value> ...]

    Commands:
      provision   Create (or top up) the bot players, their SSO tickets, credits and
                  permissions, and write the accounts file. Run it while the bots are offline.
      run         Bring the bots online and let them play for LoadBots:Run:DurationMinutes,
                  printing progress, then write a report.
      smoke       One deliberate pass over every feature with five bots; exits 1 on any
                  hard failure.

    Settings live in loadbots.json (section LoadBots); the database connection is read from
    the server's appsettings files (Turbo:Database:ConnectionString).
    """;

var command = args.FirstOrDefault()?.ToLowerInvariant();

if (command is not ("provision" or "run" or "smoke"))
{
    Console.WriteLine(USAGE);

    return command is null or "help" or "--help" or "-h" ? 0 : 1;
}

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings
    {
        Args = args[1..],
        ContentRootPath = AppContext.BaseDirectory,
        // A test tool talks to a development hotel unless told otherwise.
        EnvironmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Development,
    }
);

builder.Configuration.AddJsonFile("loadbots.json", optional: false);
builder.Configuration.AddEnvironmentVariables("LOADBOTS__");
builder.Configuration.AddCommandLine(args[1..]);

builder.Services.AddTurboDatabaseContext(builder);

var options =
    builder.Configuration.GetSection(LoadBotOptions.SECTION_NAME).Get<LoadBotOptions>()
    ?? new LoadBotOptions();

using var host = builder.Build();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var dbFactory = host.Services.GetRequiredService<IDbContextFactory<TurboDbContext>>();

using var cancel = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    // The first Ctrl+C ends the run cleanly and still writes the report.
    e.Cancel = true;
    cancel.Cancel();
};

try
{
    switch (command)
    {
        case "provision":
            await new BotProvisioner(
                dbFactory,
                options,
                loggerFactory.CreateLogger<BotProvisioner>()
            ).ProvisionAsync(cancel.Token);

            return 0;
        case "run":
            return await new LoadRun(options, await LoadKnowledgeAsync(), loggerFactory).RunAsync(
                cancel.Token
            );
        default:
            return await new SmokeRun(options, await LoadKnowledgeAsync(), loggerFactory).RunAsync(
                cancel.Token
            );
    }
}
catch (OperationCanceledException) when (cancel.IsCancellationRequested)
{
    Console.WriteLine("Cancelled.");

    return 130;
}

async Task<HotelKnowledge> LoadKnowledgeAsync()
{
    var knowledge = await HotelKnowledge.LoadAsync(dbFactory, cancel.Token);

    loggerFactory
        .CreateLogger<HotelKnowledge>()
        .LogInformation(
            "Hotel knowledge: {Models} room models, {Decor} decor offers, {Pads} pad offers, {Wired} wired box types",
            knowledge.RoomModels.Count,
            knowledge.DecorOffers.Count,
            knowledge.WalkablePadOffers.Count,
            knowledge.WiredOffers.Count
        );

    return knowledge;
}
