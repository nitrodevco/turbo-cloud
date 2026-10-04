using System;
using System.Collections.Generic;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.LoadBots;

/// <summary>
/// Settings for the bots, bound from the <c>LoadBots</c> section of <c>loadbots.json</c>; any of
/// them can be overridden on the command line (<c>--LoadBots:Bots=200</c>) or with environment
/// variables (<c>LOADBOTS__LoadBots__Bots=200</c>).
/// </summary>
public sealed class LoadBotOptions
{
    public const string SECTION_NAME = "LoadBots";

    /// <summary>The game socket host, as the client reaches it.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>The TCP game port (<c>serverOptions:TcpServer</c>).</summary>
    public int Port { get; init; } = 30000;

    /// <summary><c>tcp</c> for the Flash socket, <c>websocket</c> for the web client's socket.</summary>
    public string Transport { get; init; } = "tcp";

    /// <summary>The WebSocket endpoint (<c>serverOptions:WebSocketServer</c>).</summary>
    public string WebSocketUrl { get; init; } = "ws://127.0.0.1:9001";

    /// <summary>Where <c>provision</c> writes the bot logins and <c>run</c> reads them.</summary>
    public string AccountsFile { get; init; } = "loadbots.accounts.json";

    /// <summary>How long a bot waits for the reply to one request before calling it lost.</summary>
    public double ReplyTimeoutSeconds { get; init; } = 10;

    public ProvisionOptions Provision { get; init; } = new();

    public RunOptions Run { get; init; } = new();

    public TimeSpan ReplyTimeout => TimeSpan.FromSeconds(ReplyTimeoutSeconds);
}

public sealed class ProvisionOptions
{
    /// <summary>How many bot players to have once provisioning is done.</summary>
    public int Count { get; init; } = 50;

    /// <summary>Bot names are this and a number; keep it distinct from real players' names.</summary>
    public string NamePrefix { get; init; } = "lb_";

    /// <summary>The credits each bot is topped up to, so the catalog never refuses it.</summary>
    public int Credits { get; init; } = 1_000_000;

    /// <summary>
    /// Permission nodes granted to every bot. Saving a floor plan needs Builders Club or the
    /// first; the second allows the larger plans the architects draw.
    /// </summary>
    public List<string> GrantedPermissionNodes { get; init; } =
    [PermissionNodes.Room.FLOORPLAN_SAVE_WITHOUT_CLUB, PermissionNodes.Room.FLOORPLAN_LARGE];
}

public sealed class RunOptions
{
    /// <summary>How many bots to bring online; 0 means every provisioned account.</summary>
    public int Bots { get; init; }

    /// <summary>How long the run lasts once every bot is online.</summary>
    public double DurationMinutes { get; init; } = 10;

    /// <summary>How many bots log in per second while ramping up.</summary>
    public double RampPerSecond { get; init; } = 5;

    /// <summary>Seed for every random choice; the same seed and accounts replay the same plan.</summary>
    public int Seed { get; init; } = 1;

    /// <summary>The share of bots given each persona (relative weights).</summary>
    public Dictionary<string, double> Personas { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Decorator"] = 3,
            ["Architect"] = 1,
            ["WiredEngineer"] = 1,
            ["GameHost"] = 1,
            ["Visitor"] = 6,
        };

    /// <summary>The pause between a bot's activities, picked at random in this range.</summary>
    public int ThinkTimeMinMs { get; init; } = 500;

    public int ThinkTimeMaxMs { get; init; } = 3000;

    /// <summary>A bot reuses its rooms once it owns this many.</summary>
    public int MaxRoomsPerBot { get; init; } = 3;

    /// <summary>A builder starts picking furni up again once its room holds this many.</summary>
    public int MaxItemsPerRoom { get; init; } = 80;

    /// <summary>How often the console prints a summary.</summary>
    public double ReportIntervalSeconds { get; init; } = 10;

    /// <summary>The JSON report written at the end; <c>{time}</c> is replaced.</summary>
    public string ReportFile { get; init; } = "loadbots-report-{time}.json";
}
