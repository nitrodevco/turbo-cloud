using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record ShutdownArguments(string? When = null, RestOfLine? Reason = null);

/// <summary>
/// <c>:shutdown [minutes] [reason]</c> counts the hotel down and stops it: players are reminded
/// as it runs out, then sent home, and the host stops, which lets the grains write what they hold.
/// Given no minutes it counts down from <c>Turbo:Operations:DefaultShutdownMinutes</c>, so a
/// stray command is a warning, not an outage. <c>:shutdown cancel</c> calls it off.
/// </summary>
[Command(
    "shutdown",
    Description = "Close the hotel down after a countdown",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.SHUTDOWN)]
public sealed class ShutdownCommand(
    IHotelAvailability availability,
    IOptions<OperationsConfig> config
) : IOperatorCommand<ShutdownArguments>
{
    private const string SCHEDULED = "scheduled";
    private const string CANCELLED = "cancelled";
    private const string NOT_ACTIVE = "not_active";
    private const string BAD_MINUTES = "bad_minutes";
    private const string CONFIRM = "confirm";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [SCHEDULED] = "The hotel shuts down in %0% minutes.",
            [CANCELLED] = "The shutdown is called off.",
            [NOT_ACTIVE] = "No shutdown is counting down.",
            [BAD_MINUTES] = "Say how many minutes, from 0 to %0%, or cancel.",
            [CONFIRM] = "This shuts the hotel down in %0% minutes.",
        };

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        ShutdownArguments arguments,
        CancellationToken ct
    )
    {
        if (arguments.When?.Equals("cancel", StringComparison.OrdinalIgnoreCase) == true)
            return ValueTask.FromResult(
                availability.Current.Phase == HotelAvailabilityPhase.ShutdownScheduled
                && availability.Cancel()
                    ? CommandResult.Done(CANCELLED)
                    : CommandResult.Fail(NOT_ACTIVE)
            );

        var max = config.Value.MaxCountdownMinutes;
        var minutes = config.Value.DefaultShutdownMinutes;

        if (
            arguments.When is not null
            && (
                !int.TryParse(
                    arguments.When,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out minutes
                )
                || minutes > max
            )
        )
            return ValueTask.FromResult(
                CommandResult.Fail(BAD_MINUTES, max.ToString(CultureInfo.InvariantCulture))
            );

        // Nothing the hotel does reaches more players, so it always waits for a :confirm.
        if (!ctx.IsConfirmed)
            return ValueTask.FromResult(
                CommandResult.Confirm(CONFIRM, minutes.ToString(CultureInfo.InvariantCulture))
            );

        availability.ScheduleShutdown(
            TimeSpan.FromMinutes(minutes),
            arguments.Reason?.Text.Trim() ?? string.Empty
        );

        return ValueTask.FromResult(
            CommandResult.Done(SCHEDULED, minutes.ToString(CultureInfo.InvariantCulture))
        );
    }
}
