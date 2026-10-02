using System.Diagnostics;
using System.Diagnostics.Metrics;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Server-side timings of chat commands. A command runs inside its room's turn, so how long it
/// takes is how long the room waits. The metric is tagged with the command's registered name,
/// which is bounded by the commands that are loaded, and the outcome. Neither the span nor the
/// metric ever carries a player, the arguments or a reply: all of those are what a player typed.
/// </summary>
public static class CommandTelemetry
{
    public const string SOURCE_NAME = "Turbo.Commands";
    public const string DURATION_NAME = "turbo.command.duration";
    public const string EXECUTE = "command.execute";

    private static readonly ActivitySource SOURCE = new(SOURCE_NAME);
    private static readonly Meter METER = new(SOURCE_NAME);
    private static readonly Histogram<double> DURATION = METER.CreateHistogram<double>(
        DURATION_NAME,
        "s",
        "Chat command duration from match to reply, inside the room's turn; not client render time."
    );

    /// <summary>
    /// Starts measuring one command line. When nothing listens it costs a check and returns an
    /// inert measurement, so the chat path pays nothing for telemetry that is off.
    /// </summary>
    public static CommandMeasurement Start(string command, RoomId roomId)
    {
        if (!SOURCE.HasListeners() && !DURATION.Enabled)
            return CommandMeasurement.Inert;

        var activity = SOURCE.StartActivity(EXECUTE);

        activity?.SetTag("command.name", command);
        activity?.SetTag("room.id", roomId.Value);

        return new CommandMeasurement(activity, Stopwatch.GetTimestamp(), command);
    }

    internal static void Record(string command, CommandOutcome outcome, double seconds) =>
        DURATION.Record(
            seconds,
            new TagList { { "command", command }, { "outcome", Name(outcome) } }
        );

    /// <summary>The outcome as its tag value, without allocating one per line.</summary>
    public static string Name(CommandOutcome outcome) =>
        outcome switch
        {
            CommandOutcome.Completed => "completed",
            CommandOutcome.Refused => "refused",
            CommandOutcome.RoomLevel => "room_level",
            CommandOutcome.BindFailed => "bind_failed",
            CommandOutcome.Vetoed => "vetoed",
            CommandOutcome.Flood => "flood",
            CommandOutcome.Canceled => "canceled",
            CommandOutcome.AwaitingConfirmation => "confirm",
            CommandOutcome.Failed => "failed",
            CommandOutcome.Partial => "partial",
            _ => "error",
        };
}
