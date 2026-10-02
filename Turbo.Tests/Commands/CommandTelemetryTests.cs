using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Events;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// Telemetry sources are process-wide and test classes run in parallel, so every test here uses
/// commands no other test registers and reads back only what they emitted.
/// </summary>
public sealed class CommandTelemetryTests : IDisposable
{
    private const string OK = "telemetryok";
    private const string BINDS = "telemetrybinds";
    private const string BREAKS = "telemetrybreaks";

    private readonly CommandRoomFixture _room = new();
    private readonly List<Activity> _activities = [];
    private readonly List<(double Seconds, Dictionary<string, object?> Tags)> _measurements = [];
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener = new();

    public CommandTelemetryTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CommandTelemetry.SOURCE_NAME,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (IsMine(activity.GetTagItem("command.name")))
                    lock (_activities)
                        _activities.Add(activity);
            },
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == CommandTelemetry.SOURCE_NAME)
                listener.EnableMeasurementEvents(instrument);
        };
        _meterListener.SetMeasurementEventCallback<double>(
            (_, value, tags, _) =>
            {
                var asDictionary = tags.ToArray().ToDictionary(x => x.Key, x => x.Value);

                if (IsMine(asDictionary.GetValueOrDefault("command")))
                    lock (_measurements)
                        _measurements.Add((value, asDictionary));
            }
        );
        _meterListener.Start();
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }

    private static bool IsMine(object? name) => name is OK or BINDS or BREAKS or "telemetryfailure";

    private IEnumerable<string?> Outcomes() =>
        _measurements.Select(x => x.Tags["outcome"] as string);

    [Fact]
    public async Task ACompletedCommand_RecordsOneSpanAndOneDuration_WithOnlyBoundedTags()
    {
        _room.Commands.Register([new TelemetryOkCommand()]);
        _room.AddPlayer(2, "command.telemetryok");

        await _room.SayAsync(2, ":telemetryok a very private thing someone typed");

        var activity = _activities.Should().ContainSingle().Which;
        activity.OperationName.Should().Be(CommandTelemetry.EXECUTE);
        activity.GetTagItem("command.outcome").Should().Be("completed");
        activity
            .TagObjects.Select(x => x.Key)
            .Should()
            .BeEquivalentTo("command.name", "room.id", "command.outcome");
        activity.TagObjects.Should().NotContain(x => x.Value!.ToString()!.Contains("private"));

        var measurement = _measurements.Should().ContainSingle().Which;
        measurement
            .Tags.Should()
            .BeEquivalentTo(
                new Dictionary<string, object?> { ["command"] = OK, ["outcome"] = "completed" }
            );
        measurement.Seconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task EveryWayALineCanEnd_HasItsOwnOutcomeValue()
    {
        _room.Commands.Register([
            new TelemetryOkCommand(),
            new TelemetryBindsCommand(),
            new TelemetryBreaksCommand(),
        ]);
        _room.AddPlayer(2, "command.telemetrybinds", "command.telemetrybreaks");
        _room.AddPlayer(3, "command.telemetryok");
        _room.Events.On<CommandExecutingEvent>(e =>
        {
            if (e.Descriptor.Name == OK)
                e.Cancel();
        });

        await _room.SayAsync(2, ":telemetryok"); // no node
        await _room.SayAsync(2, ":telemetrybinds many"); // does not bind
        await _room.SayAsync(2, ":telemetrybreaks"); // throws
        await _room.SayAsync(3, ":telemetryok"); // vetoed

        Outcomes().Should().Equal("refused", "bind_failed", "error", "vetoed");
    }

    [Fact]
    public async Task ACommandOverTheFloodLimit_IsRecordedAsFlood()
    {
        _room.Commands.Register([new TelemetryOkCommand()]);
        _room.AddPlayer(2, "command.telemetryok");

        for (var i = 0; i < _room.FloodLimit + 1; i++)
            await _room.SayAsync(2, ":telemetryok");

        Outcomes().Should().HaveCount(_room.FloodLimit + 1);
        Outcomes().Last().Should().Be("flood");
        Outcomes().Take(_room.FloodLimit).Should().AllBe("completed");
    }

    [Fact]
    public void AMeasurementRecordsOnce_EvenWhenDisposedAfterCompleting()
    {
        using (var measurement = CommandTelemetry.Start(OK, 1))
        {
            measurement.Complete(CommandOutcome.Completed);
            measurement.Complete(CommandOutcome.Error);
        }

        Outcomes().Should().Equal("completed");
    }

    [Fact]
    public async Task ACommandReturningFailure_RecordsFailedInsteadOfCompleted()
    {
        _room.Commands.Register([new TelemetryFailureCommand()]);
        _room.AddPlayer(2, "command.telemetryfailure");

        await _room.SayAsync(2, ":telemetryfailure");

        Outcomes().Should().Equal("failed");
        _activities
            .Should()
            .ContainSingle()
            .Which.GetTagItem("command.outcome")
            .Should()
            .Be("failed");
    }

    [Fact]
    public void AMeasurementNobodyCompleted_IsRecordedAsAnErrorRatherThanLost()
    {
        using (CommandTelemetry.Start(OK, 1)) { }

        Outcomes().Should().Equal("error");
    }
}

[Command("telemetryok")]
[RequiresPermission("command.telemetryok")]
public sealed class TelemetryOkCommand : ICommand<ProbeArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        ProbeArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

[Command("telemetrybinds")]
[RequiresPermission("command.telemetrybinds")]
public sealed class TelemetryBindsCommand : ICommand<CountArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        CountArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

[Command("telemetrybreaks")]
[RequiresPermission("command.telemetrybreaks")]
public sealed class TelemetryBreaksCommand : ICommand<NoArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => throw new InvalidOperationException("secret");
}
