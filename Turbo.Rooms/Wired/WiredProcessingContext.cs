using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

namespace Turbo.Rooms.Wired;

public sealed class WiredProcessingContext
{
    public required RoomGrain Room { get; init; }
    public required RoomEvent Event { get; init; }
    public required WiredStack Stack { get; init; }
    public required FurnitureWiredTriggerLogic Trigger { get; init; }
    public Dictionary<string, object?> Variables { get; } = [];
    public WiredPolicy Policy { get; } = new WiredPolicy();
    public WiredSelectionSet Selected { get; } = new WiredSelectionSet();
    public WiredSelectionSet SelectorPool { get; } = new WiredSelectionSet();

    private readonly Dictionary<int, WiredSelectionSet> _wiredSelectionCache = [];

    public async Task<WiredSelectionSet> GetWiredSelectionSetAsync(
        FurnitureWiredLogic wired,
        CancellationToken ct
    )
    {
        if (_wiredSelectionCache.TryGetValue(wired.Id, out var cached))
            return cached;

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
                                    if (!Room._liveState.FloorItemsById.ContainsKey(id))
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

        _wiredSelectionCache[wired.Id] = set;

        return set;
    }

    public async Task<WiredSelectionSet> GetEffectiveSelectionAsync(
        FurnitureWiredLogic wired,
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

    public WiredContextSnapshot GetSnapshot() =>
        new()
        {
            Variables = new Dictionary<string, object?>(Variables),
            Selected = Selected.GetSnapshot(),
        };
}
