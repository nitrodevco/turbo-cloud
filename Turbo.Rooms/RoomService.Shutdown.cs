using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Orleans;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
    /// <summary>Each room being saved holds a database connection while it writes.</summary>
    private const int ROOMS_SAVED_AT_ONCE = 16;

    public async Task SaveAndUnloadAllRoomsAsync(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var roomIds = await _grainFactory
            .GetRoomDirectoryGrain()
            .GetActiveRoomIdsAsync(ct)
            .ConfigureAwait(false);
        var saved = 0;
        var failed = 0;

        try
        {
            await Parallel
                .ForEachAsync(
                    roomIds,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = ROOMS_SAVED_AT_ONCE,
                        CancellationToken = ct,
                    },
                    async (roomId, token) =>
                    {
                        try
                        {
                            var written = await _grainFactory
                                .GetRoomGrain(roomId)
                                .SaveAndUnloadAsync(token)
                                .ConfigureAwait(false);

                            Interlocked.Increment(ref written ? ref saved : ref failed);
                        }
                        catch (Exception ex) when (!token.IsCancellationRequested)
                        {
                            Interlocked.Increment(ref failed);

                            _logger.LogError(
                                ex,
                                "Failed to save room {RoomId} as the server stops",
                                roomId
                            );
                        }
                    }
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The host's shutdown timeout: what was not reached is counted below.
        }

        var elapsedMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var notReached = roomIds.Length - saved - failed;

        if (failed == 0 && notReached == 0)
        {
            _logger.LogInformation(
                "Saved {Count} active rooms in {ElapsedMs} ms",
                roomIds.Length,
                elapsedMs
            );

            return;
        }

        _logger.LogError(
            "Saved {Saved} of {Count} active rooms in {ElapsedMs} ms: {Failed} could not be written and {NotReached} were not reached before the shutdown timeout; their latest changes are lost",
            saved,
            roomIds.Length,
            elapsedMs,
            failed,
            notReached
        );
    }
}
