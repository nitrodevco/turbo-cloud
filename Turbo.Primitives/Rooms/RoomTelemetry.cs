using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms;

/// <summary>Server-side room timings. Metrics never include room or player identities.</summary>
public static class RoomTelemetry
{
    public const string SOURCE_NAME = "Turbo.Rooms";
    public const string DURATION_NAME = "turbo.room.operation.duration";
    public const string STREAM_DELAY_NAME = "turbo.room.stream.delivery.delay";
    public const string DIRECT_ENTRY = "room.entry.direct";
    public const string NAVIGATOR_ENTRY = "room.entry.navigator";
    public const string ACCESS = "room.entry.access";
    public const string ACTIVATE = "room.activate";
    public const string HYDRATE = "room.hydrate";
    public const string PREPARE_PLAYER = "room.entry.prepare_player";
    public const string ENTRY_VIEW = "room.entry.view";
    public const string INITIAL_PACKETS = "room.entry.queue_initial_packets";
    public const string MEMBERSHIP = "room.entry.membership";
    public const string SUBSCRIBE = "room.entry.subscribe";
    public const string AVATAR = "room.entry.avatar";
    public const string PLAYER_SUMMARY = "room.entry.player_summary";
    public const string LOAD_MAP = "room.load.map";
    public const string LOAD_FURNITURE = "room.load.furniture";
    public const string LOAD_PETS = "room.load.pets";
    public const string LOAD_BOTS = "room.load.bots";
    public const string LOAD_PERMISSIONS = "room.load.permissions";

    private static readonly ActivitySource SOURCE = new(SOURCE_NAME);
    private static readonly Meter METER = new(SOURCE_NAME);
    private static readonly Histogram<double> DURATION = METER.CreateHistogram<double>(
        DURATION_NAME,
        "s",
        "Server operation duration, including awaited grain calls; not client render time."
    );
    private static readonly Histogram<double> STREAM_DELAY = METER.CreateHistogram<double>(
        STREAM_DELAY_NAME,
        "s",
        "Room stream publication to eligible presence delivery; requires synchronized silo clocks."
    );

    public static Task MeasureAsync(string stage, RoomId roomId, Func<Task> action) =>
        !SOURCE.HasListeners() && !DURATION.Enabled
            ? action()
            : MeasureCoreAsync(
                stage,
                roomId,
                async () =>
                {
                    await action().ConfigureAwait(false);
                    return true;
                }
            );

    public static Task<T> MeasureAsync<T>(string stage, RoomId roomId, Func<Task<T>> action) =>
        !SOURCE.HasListeners() && !DURATION.Enabled
            ? action()
            : MeasureCoreAsync(stage, roomId, action);

    private static async Task<T> MeasureCoreAsync<T>(
        string stage,
        RoomId roomId,
        Func<Task<T>> action
    )
    {
        using var activity = SOURCE.StartActivity(stage);
        activity?.SetTag("room.id", roomId.Value);
        var started = Stopwatch.GetTimestamp();
        var outcome = "completed";
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            outcome = "canceled";
            activity?.SetTag("operation.canceled", true);
            throw;
        }
        catch (Exception ex)
        {
            outcome = "error";
            // Exception messages can contain user input or connection details.
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", ex.GetType().FullName);
            throw;
        }
        finally
        {
            activity?.SetTag("operation.outcome", outcome);
            DURATION.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds,
                new TagList { { "stage", stage }, { "outcome", outcome } }
            );
        }
    }

    public static long GetPublicationTimestamp() =>
        STREAM_DELAY.Enabled ? DateTime.UtcNow.Ticks : 0;

    public static void RecordEntryAccess(RoomEntryAccessType access)
    {
        if (Activity.Current?.Source.Name == SOURCE_NAME)
            Activity.Current.SetTag("room.entry.access", access.ToString());
    }

    public static void RecordStreamDelivery(long publishedAtUtcTicks)
    {
        if (!STREAM_DELAY.Enabled || publishedAtUtcTicks <= 0)
            return;

        var now = DateTime.UtcNow.Ticks;
        // Missing timestamps from older silos and negative clock skew are not measurements.
        if (publishedAtUtcTicks <= now)
            STREAM_DELAY.Record(TimeSpan.FromTicks(now - publishedAtUtcTicks).TotalSeconds);
    }
}
