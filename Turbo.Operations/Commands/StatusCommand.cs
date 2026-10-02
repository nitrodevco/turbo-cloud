using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

/// <summary>
/// <c>:status</c>. How the hotel is running, in one notice: how long, how many players and rooms,
/// the silos the cluster sees, memory, and whether a maintenance or a shutdown is coming. The
/// detail behind it is in the telemetry; this is the glance an operator takes first.
/// </summary>
[Command(
    "status",
    Description = "See how the hotel is running",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.STATUS)]
public sealed class StatusCommand(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IHotelAvailability availability,
    TimeProvider timeProvider
) : IOperatorCommand<NoArguments>
{
    private const string REPORTED = "reported";

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    )
    {
        using var process = Process.GetCurrentProcess();

        var uptime = timeProvider.GetUtcNow().UtcDateTime - process.StartTime.ToUniversalTime();
        var rooms = await grainFactory.GetRoomDirectoryGrain().GetActiveRoomIdsAsync(ct);
        var hosts = await grainFactory.GetGrain<IManagementGrain>(0).GetHosts(false);
        var active = hosts.Count(x => x.Value == SiloStatus.Active);
        var current = availability.Current;

        var lines = new List<string>
        {
            $"Turbo {Assembly.GetEntryAssembly()?.GetName().Version}, up {Format(uptime)}",
            $"Players online: {sessionGateway.GetOnlinePlayerIds().Count}, rooms loaded: {rooms.Length}",
            $"Silos active: {active} of {hosts.Count}",
            $"Memory: {process.WorkingSet64 / 1024 / 1024} MB in use, {GC.GetTotalMemory(false) / 1024 / 1024} MB managed",
            current.Phase switch
            {
                HotelAvailabilityPhase.Open => "The hotel is open",
                HotelAvailabilityPhase.Maintenance => "The hotel is in maintenance",
                HotelAvailabilityPhase.ShuttingDown => "The hotel is shutting down",
                HotelAvailabilityPhase.MaintenanceScheduled => $"Maintenance starts {At(current)}",
                _ => $"Shutdown at {At(current)}",
            },
        };

        await ctx.Executor.NoticeAsync(lines, ct);

        return CommandResult.Done(REPORTED);
    }

    private static string At(HotelAvailabilitySnapshot snapshot) =>
        snapshot.AtUtc?.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "soon";

    private static string Format(TimeSpan span) =>
        span.TotalDays >= 1
            ? $"{(int)span.TotalDays} d {span.Hours} h"
            : $"{span.Hours} h {span.Minutes} min";
}
