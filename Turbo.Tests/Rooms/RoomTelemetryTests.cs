using System.Diagnostics;
using System.Diagnostics.Metrics;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

[CollectionDefinition("Room telemetry", DisableParallelization = true)]
public class RoomTelemetryCollection { }

[Collection("Room telemetry")]
public class RoomTelemetryTests
{
    [Fact]
    public async Task DirectEntry_DeniedByRoomServiceKeepsProtocolOrderAndNestsAccessSpans()
    {
        var harness = new PacketHarness();
        var activities = new List<Activity>();
        using var listener = Listen(activities);
        harness.Fakes.Handlers["GetPendingRoomAsync"] = _ =>
            Task.FromResult(
                new Turbo.Primitives.Rooms.Snapshots.RoomPendingSnapshot
                {
                    RoomId = -1,
                    State = RoomEntryState.None,
                }
            );
        harness.Fakes.Handlers["GetPendingRoomEntryAsync"] = _ =>
            Task.FromResult(Turbo.Primitives.Rooms.Snapshots.RoomEntrySnapshot.Default);
        harness.Fakes.Handlers["CheckEntryAccessAsync"] = _ =>
            Task.FromResult(RoomEntryAccessType.Closed);

        var packets = await harness.SendAsync(
            PacketHarness.Incoming("OpenFlatConnectionMessageEvent"),
            PacketHarness.Payload(w => w.Int(42).String("private-password").Int(-1))
        );

        Assert.Equal(
            [
                PacketHarness.Outgoing("OpenConnectionMessageComposer"),
                PacketHarness.Outgoing("CantConnectMessageComposer"),
                PacketHarness.Outgoing("CloseConnectionMessageComposer"),
            ],
            packets.Select(packet => (int)packet.Header)
        );
        var direct = Assert.Single(
            activities,
            activity => activity.OperationName == RoomTelemetry.DIRECT_ENTRY
        );
        Assert.DoesNotContain(
            activities,
            activity => activity.OperationName == RoomTelemetry.ACTIVATE
        );
        var access = Assert.Single(
            activities,
            activity => activity.OperationName == RoomTelemetry.ACCESS
        );
        Assert.Equal(direct.TraceId, access.TraceId);
        Assert.Equal(direct.SpanId, access.ParentSpanId);
    }

    [Fact]
    public async Task DirectEntry_WhenAccessIsCanceledRethrowsAndMarksCanceled()
    {
        var harness = new PacketHarness();
        var activities = new List<Activity>();
        using var listener = Listen(activities);
        harness.Fakes.Handlers["GetPendingRoomAsync"] = _ =>
            Task.FromResult(
                new Turbo.Primitives.Rooms.Snapshots.RoomPendingSnapshot
                {
                    RoomId = -1,
                    State = RoomEntryState.None,
                }
            );
        harness.Fakes.Handlers["GetPendingRoomEntryAsync"] = _ =>
            Task.FromResult(Turbo.Primitives.Rooms.Snapshots.RoomEntrySnapshot.Default);
        harness.Fakes.Handlers["CheckEntryAccessAsync"] = _ =>
            Task.FromCanceled<RoomEntryAccessType>(new CancellationToken(canceled: true));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.SendAsync(
                PacketHarness.Incoming("OpenFlatConnectionMessageEvent"),
                PacketHarness.Payload(w => w.Int(42).String("password").Int(-1))
            )
        );

        Assert.Equal(
            "canceled",
            Assert
                .Single(activities, a => a.OperationName == RoomTelemetry.ACCESS)
                .GetTagItem("operation.outcome")
        );
        Assert.Equal(
            "canceled",
            Assert
                .Single(activities, a => a.OperationName == RoomTelemetry.DIRECT_ENTRY)
                .GetTagItem("operation.outcome")
        );
    }

    [Fact]
    public async Task MeasureAsync_RecordsLowCardinalityOutcomeAndRethrowsWithoutMessageTag()
    {
        var activities = new List<Activity>();
        var measurements = new List<(double Value, KeyValuePair<string, object?>[] Tags)>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RoomTelemetry.SOURCE_NAME,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == RoomTelemetry.SOURCE_NAME)
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>(
            (_, value, tags, _) => measurements.Add((value, tags.ToArray()))
        );
        meterListener.Start();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RoomTelemetry.MeasureAsync(
                "test.failure",
                (RoomId)987654,
                () => Task.FromException(new InvalidOperationException("secret password"))
            )
        );

        Assert.Equal("secret password", exception.Message);
        var activity = Assert.Single(activities);
        Assert.Equal("error", activity.GetTagItem("operation.outcome"));
        Assert.Equal(typeof(InvalidOperationException).FullName, activity.GetTagItem("error.type"));
        Assert.DoesNotContain(
            activity.TagObjects,
            tag => tag.Value?.ToString()?.Contains("secret password") == true
        );
        var measurement = Assert.Single(measurements);
        Assert.Equal(
            new Dictionary<string, object?> { ["stage"] = "test.failure", ["outcome"] = "error" },
            measurement.Tags.ToDictionary(tag => tag.Key, tag => tag.Value)
        );
    }

    [Fact]
    public void RecordStreamDelivery_IgnoresLegacyAndFutureTimesAndRecordsPastTime()
    {
        var measurements = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == RoomTelemetry.STREAM_DELAY_NAME)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        RoomTelemetry.RecordStreamDelivery(0);
        RoomTelemetry.RecordStreamDelivery(-1);
        RoomTelemetry.RecordStreamDelivery(DateTime.UtcNow.AddMinutes(1).Ticks);
        RoomTelemetry.RecordStreamDelivery(DateTime.UtcNow.AddSeconds(-1).Ticks);

        var delay = Assert.Single(measurements);
        Assert.True(delay >= 0.9);
    }

    private static ActivityListener Listen(List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RoomTelemetry.SOURCE_NAME,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
