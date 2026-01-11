using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Grains.Systems;

public sealed class RoomWiredVariableSystem(RoomGrain roomGrain) : IRoomEventListener
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private readonly Dictionary<string, WiredVariableRegistration> _byKey = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<int, HashSet<string>> _keysByBoxId = [];

    private readonly HashSet<int> _dirtyBoxIds = [];

    public async Task ProcessBoxesAsync(long now, CancellationToken ct)
    {
        if (_dirtyBoxIds.Count == 0)
            return;

        var dirtyBoxIds = _dirtyBoxIds.ToList();
        _dirtyBoxIds.Clear();

        foreach (var boxId in dirtyBoxIds)
            await ProcessBoxIdAsync(boxId, ct);
    }

    private async Task ProcessBoxIdAsync(int boxId, CancellationToken ct)
    {
        if (
            !_roomGrain._liveState.FloorItemsById.TryGetValue(boxId, out var floorItem)
            || floorItem.Logic is not FurnitureWiredVariableLogic varLogic
        )
            return;

        await varLogic.LoadWiredAsync(ct);

        var regs = varLogic.GetVariableRegistrations();

        ResetBox(boxId, regs);
    }

    public void ResetBox(int boxId, List<WiredVariableRegistration> regs)
    {
        if (_keysByBoxId.TryGetValue(boxId, out var prevKeys))
        {
            foreach (var key in prevKeys)
                _byKey.Remove(key);
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reg in regs)
        {
            _byKey[reg.Definition.Key] = reg;

            keys.Add(reg.Definition.Key);
        }

        _keysByBoxId[boxId] = keys;
    }

    public void RemoveBox(int boxId)
    {
        if (!_keysByBoxId.TryGetValue(boxId, out var keys))
            return;

        foreach (var key in keys)
            _byKey.Remove(key);

        _keysByBoxId.Remove(boxId);
    }

    public bool TryGet(string key, out WiredVariableRegistration? reg) =>
        _byKey.TryGetValue(key, out reg);

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not null)
        {
            if (evt is WiredVariableBoxChangedEvent boxEvt)
            {
                foreach (var boxId in boxEvt.BoxIds)
                    _dirtyBoxIds.Add(boxId);
            }
        }

        return Task.CompletedTask;
    }
}
