using System;
using System.Diagnostics;

namespace Turbo.Primitives.Commands;

/// <summary>
/// One command line being measured. <see cref="Complete"/> ends the span and records the
/// duration once; the inert measurement <see cref="CommandTelemetry.Start"/> returns when nothing
/// listens does nothing.
/// </summary>
public sealed class CommandMeasurement : IDisposable
{
    /// <summary>What <see cref="CommandTelemetry.Start"/> hands out when nothing listens: shared, so telemetry that is off allocates nothing.</summary>
    internal static CommandMeasurement Inert { get; } = new(null, 0, null);

    private readonly Activity? _activity;
    private readonly long _started;
    private readonly string? _command;
    private bool _completed;

    internal CommandMeasurement(Activity? activity, long started, string? command)
    {
        _activity = activity;
        _started = started;
        _command = command;
    }

    public void Complete(CommandOutcome outcome)
    {
        if (_command is null || _completed)
            return;

        _completed = true;

        _activity?.SetTag("command.outcome", CommandTelemetry.Name(outcome));

        if (outcome == CommandOutcome.Error)
            _activity?.SetStatus(ActivityStatusCode.Error);

        CommandTelemetry.Record(_command, outcome, Stopwatch.GetElapsedTime(_started).TotalSeconds);
    }

    /// <summary>
    /// Ends the span. A measurement never completed is a line that ended in an exception nobody
    /// classified, and is recorded as an error rather than lost.
    /// </summary>
    public void Dispose()
    {
        if (_command is null)
            return;

        Complete(CommandOutcome.Error);
        _activity?.Dispose();
    }
}
