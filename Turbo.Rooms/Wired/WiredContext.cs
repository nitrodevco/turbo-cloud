using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired;

public abstract class WiredContext(RoomGrain roomGrain) : IWiredContext
{
    protected RoomGrain _roomGrain = roomGrain;

    public IRoomGrain Room => _roomGrain;

    public IWiredPolicy Policy { get; init; } = new WiredPolicy();
    public IWiredSelectionSet Selected { get; init; } = new WiredSelectionSet();
    public IWiredSelectionSet SelectorPool { get; init; } = new WiredSelectionSet();
    public Dictionary<string, int> Variables { get; init; } = [];
    public CancellationToken CancellationToken { get; init; }

    public bool TryGetContextVariable(string key, out int value)
    {
        if (!Variables.TryGetValue(key, out var intValue))
        {
            value = intValue;

            return true;
        }

        value = default;

        return false;
    }

    public async Task<bool> SetContextVariableAsync(string key, int value)
    {
        Variables[key] = value;

        return await Task.FromResult(true);
    }

    public Task<IWiredSelectionSet> GetWiredSelectionSetAsync(IWiredBox wired, CancellationToken ct)
    {
        var set = new WiredSelectionSet();

        foreach (var source in wired.GetFurniSources())
        {
            foreach (var sourceType in source)
            {
                switch (sourceType)
                {
                    case WiredFurniSourceType.SelectedItems:
                        {
                            var stuffIds = wired.WiredData?.StuffIds;

                            if (stuffIds is not null && stuffIds.Count > 0)
                            {
                                foreach (var id in stuffIds)
                                {
                                    if (!_roomGrain._state.ItemsById.ContainsKey(id))
                                        continue;

                                    set.SelectedFurniIds.Add(id);
                                }
                            }

                            var stuffIds2 = wired.WiredData?.StuffIds2;

                            if (stuffIds2 is not null && stuffIds2.Count > 0)
                            {
                                foreach (var id in stuffIds2)
                                {
                                    if (!_roomGrain._state.ItemsById.ContainsKey(id))
                                        continue;

                                    set.SelectedFurniIds.Add(id);
                                }
                            }
                        }
                        break;
                    case WiredFurniSourceType.TriggeredItem:
                        set.SelectedFurniIds.UnionWith(Selected.SelectedFurniIds);
                        break;
                }
            }
        }

        foreach (var source in wired.GetPlayerSources())
        {
            foreach (var sourceType in source)
            {
                //switch (sourceType) { }
            }
        }

        return Task.FromResult<IWiredSelectionSet>(set);
    }

    public async Task<IWiredSelectionSet> GetEffectiveSelectionAsync(
        IWiredBox wired,
        CancellationToken ct
    )
    {
        var result = new WiredSelectionSet();
        var set = await GetWiredSelectionSetAsync(wired, ct);

        foreach (var source in wired.GetFurniSources())
        {
            foreach (var sourceType in source)
            {
                switch (sourceType)
                {
                    case WiredFurniSourceType.SelectedItems:
                        result.SelectedFurniIds.UnionWith(set.SelectedFurniIds);
                        break;
                    case WiredFurniSourceType.SelectorItems:
                        result.SelectedFurniIds.UnionWith(SelectorPool.SelectedFurniIds);
                        break;
                    case WiredFurniSourceType.TriggeredItem:
                        result.SelectedFurniIds.UnionWith(Selected.SelectedFurniIds);
                        break;
                }
            }
        }

        foreach (var source in wired.GetPlayerSources())
        {
            foreach (var sourceType in source)
            {
                //switch (sourceType) { }
            }
        }

        return result;
    }

    public virtual WiredContextSnapshot GetSnapshot() =>
        new()
        {
            Variables = new Dictionary<string, int>(Variables),
            Selected = Selected.GetSnapshot(),
        };
}
