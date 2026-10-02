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

public sealed record MaintenanceArguments(string When, RestOfLine? Reason = null);

/// <summary>
/// <c>:maintenance 10 [reason]</c> counts the hotel down to maintenance: players are reminded as
/// it runs out, and at zero everyone without <c>hotel.maintenance.bypass</c> is sent home and
/// kept out. <c>:maintenance 0</c> starts it now. <c>:maintenance off</c> calls a countdown off
/// or ends a maintenance.
/// </summary>
[Command(
    "maintenance",
    Description = "Put the hotel into maintenance after a countdown, or end it",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.MAINTENANCE)]
public sealed class MaintenanceCommand(
    IHotelAvailability availability,
    IOptions<OperationsConfig> config
) : IOperatorCommand<MaintenanceArguments>
{
    private const string SCHEDULED = "scheduled";
    private const string ENDED = "ended";
    private const string NOT_ACTIVE = "not_active";
    private const string BLOCKED = "blocked";
    private const string BAD_MINUTES = "bad_minutes";
    private const string CONFIRM = "confirm";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [SCHEDULED] = "Maintenance starts in %0% minutes.",
            [ENDED] = "The hotel is open again.",
            [NOT_ACTIVE] = "There is no maintenance, and none is counting down.",
            [BLOCKED] = "A shutdown is counting down; call it off first with :shutdown cancel.",
            [BAD_MINUTES] = "Say how many minutes, from 0 to %0%, or off.",
            [CONFIRM] = "This puts the hotel into maintenance in %0% minutes.",
        };

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        MaintenanceArguments arguments,
        CancellationToken ct
    )
    {
        if (arguments.When.Equals("off", StringComparison.OrdinalIgnoreCase))
            return ValueTask.FromResult(
                availability.Current.Phase
                    is HotelAvailabilityPhase.MaintenanceScheduled
                        or HotelAvailabilityPhase.Maintenance
                && availability.Cancel()
                    ? CommandResult.Done(ENDED)
                    : CommandResult.Fail(NOT_ACTIVE)
            );

        var max = config.Value.MaxCountdownMinutes;

        if (
            !int.TryParse(
                arguments.When,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minutes
            )
            || minutes > max
        )
            return ValueTask.FromResult(
                CommandResult.Fail(BAD_MINUTES, max.ToString(CultureInfo.InvariantCulture))
            );

        // It sends players home, so it always waits for a :confirm.
        if (!ctx.IsConfirmed)
            return ValueTask.FromResult(
                CommandResult.Confirm(CONFIRM, minutes.ToString(CultureInfo.InvariantCulture))
            );

        return ValueTask.FromResult(
            availability.ScheduleMaintenance(
                TimeSpan.FromMinutes(minutes),
                arguments.Reason?.Text.Trim() ?? string.Empty
            )
                ? CommandResult.Done(SCHEDULED, minutes.ToString(CultureInfo.InvariantCulture))
                : CommandResult.Fail(BLOCKED)
        );
    }
}
