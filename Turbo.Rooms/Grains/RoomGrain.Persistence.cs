using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    /// <summary>
    /// Hands everything that changed to the persistence grain once per
    /// <c>RoomConfig.DirtyItemsTickMs</c>, the interval that grain writes on: handing it over on
    /// every 50 ms tick bought nothing but twenty grain calls a second.
    /// </summary>
    private Task HandOverToPersistenceIfDueAsync(long now, CancellationToken ct)
    {
        if (now < _state.NextPersistenceBoundaryMs)
            return Task.CompletedTask;

        while (now >= _state.NextPersistenceBoundaryMs)
            _state.NextPersistenceBoundaryMs += _roomConfig.DirtyItemsTickMs;

        return HandOverToPersistenceAsync(ct);
    }

    /// <summary>
    /// Snapshots what is dirty as it stands now and hands it over. An item, pet or bot that left
    /// the room since it changed is skipped: its leaving wrote it already.
    /// </summary>
    private async Task HandOverToPersistenceAsync(CancellationToken ct)
    {
        var persistence = _grainFactory.GetRoomPersistenceGrain(_state.RoomId);
        var handOvers = new List<Task>(4);

        if (_state.DirtyItemIds.Count > 0)
        {
            var items = new List<RoomItemSnapshot>(_state.DirtyItemIds.Count);

            foreach (var itemId in _state.DirtyItemIds)
            {
                if (_state.ItemsById.TryGetValue(itemId, out var item))
                    items.Add(item.GetSnapshot());
            }

            _state.DirtyItemIds.Clear();

            if (items.Count > 0)
                handOvers.Add(persistence.EnqueueDirtyItemsAsync(_state.RoomId, items, ct));
        }

        if (PetModule.TakeDirtySnapshots() is { Count: > 0 } pets)
            handOvers.Add(persistence.EnqueueDirtyPetsAsync(pets, ct));

        if (BotModule.TakeDirtySnapshots() is { Count: > 0 } bots)
            handOvers.Add(persistence.EnqueueDirtyBotsAsync(bots, ct));

        if (_state.PendingChatlogs.Count > 0)
        {
            var chatlogs = new List<RoomChatlogSnapshot>(_state.PendingChatlogs);

            _state.PendingChatlogs.Clear();

            handOvers.Add(persistence.EnqueueChatlogsAsync(chatlogs, ct));
        }

        // Four different queues, so their order does not matter; each list keeps its own.
        await Task.WhenAll(handOvers);
    }

    /// <summary>
    /// Keeps a chat line for the next hand-over. Bounded like the persistence grain's own
    /// queue, so a room whose persistence grain is unreachable cannot grow it without end.
    /// </summary>
    internal void QueueChatlog(RoomChatlogSnapshot snapshot)
    {
        if (_state.PendingChatlogs.Count >= _roomConfig.MaxPendingChatlogs)
        {
            _logger.LogWarning(
                "Chatlog buffer of room {RoomId} is full ({Max}); dropping the oldest line",
                _state.RoomId,
                _roomConfig.MaxPendingChatlogs
            );

            _state.PendingChatlogs.Dequeue();
        }

        _state.PendingChatlogs.Enqueue(snapshot);
    }
}
