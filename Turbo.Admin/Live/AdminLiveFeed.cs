using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Availability;

namespace Turbo.Admin.Live;

/// <summary>
/// The hotel's changes, gathered for the panel's live streams: the event handlers here note what
/// changed, and once a beat (<see cref="AdminConfig.LiveBatchMs"/>) everything noted goes to every
/// open stream as one <see cref="LiveChangesMessage"/>. Maintenance and shutdowns are read off
/// <see cref="IHotelAvailability"/> each beat instead, being state in this process. With no stream
/// open nothing is gathered.
/// </summary>
public sealed class AdminLiveFeed(IOptions<AdminConfig> config, IHotelAvailability availability)
    : BackgroundService
{
    /// <summary>
    /// Messages a stream may fall behind by. One that falls further is ended rather than sent less
    /// than happened: the panel reconnects and fetches everything again.
    /// </summary>
    private const int STREAM_BACKLOG = 16;

    private readonly AdminConfig _config = config.Value;
    private readonly Lock _gate = new();
    private readonly List<Channel<LiveChangesMessage>> _streams = [];
    private readonly HashSet<int> _rooms = [];
    private readonly HashSet<int> _players = [];
    private readonly HashSet<int> _permissions = [];

    private bool _dashboard;
    private HotelAvailabilitySnapshot? _availability;

    /// <summary>A stream of what changes from now on; disposing it stops the stream.</summary>
    internal LiveSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<LiveChangesMessage>(
            new BoundedChannelOptions(STREAM_BACKLOG) { SingleReader = true }
        );

        lock (_gate)
            _streams.Add(channel);

        return new LiveSubscription(channel.Reader, () => Remove(channel));
    }

    /// <summary>Notes a change, for the next beat. Ids are left out when not given.</summary>
    internal void Note(
        bool dashboard = false,
        int? room = null,
        int? player = null,
        int? permissions = null
    )
    {
        lock (_gate)
        {
            if (_streams.Count == 0)
                return;

            _dashboard |= dashboard;

            if (room is { } roomId)
                _rooms.Add(roomId);

            if (player is { } playerId)
                _players.Add(playerId);

            if (permissions is { } permissionsId)
                _permissions.Add(permissionsId);
        }
    }

    /// <summary>One beat: what was noted since the last goes to every open stream.</summary>
    internal void Flush()
    {
        LiveChangesMessage message;
        Channel<LiveChangesMessage>[] streams;

        lock (_gate)
        {
            var current = availability.Current;

            if (_availability is not null && !_availability.Equals(current))
                _dashboard = true;

            _availability = current;

            if (
                _streams.Count == 0
                || (
                    !_dashboard
                    && _rooms.Count == 0
                    && _players.Count == 0
                    && _permissions.Count == 0
                )
            )
                return;

            message = new LiveChangesMessage(
                _dashboard,
                [.. _rooms],
                [.. _players],
                [.. _permissions]
            );
            streams = [.. _streams];
            _dashboard = false;
            _rooms.Clear();
            _players.Clear();
            _permissions.Clear();
        }

        foreach (var stream in streams)
        {
            if (stream.Writer.TryWrite(message))
                continue;

            stream.Writer.TryComplete();
            Remove(stream);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_config.LiveBatchMs));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                Flush();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping, and the streams with it.
        }
    }

    private void Remove(Channel<LiveChangesMessage> stream)
    {
        lock (_gate)
            _streams.Remove(stream);
    }
}
