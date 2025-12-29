using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

internal sealed class WiredContext : IWiredContext
{
    public required IRoomGrain Room { get; init; }
    public required RoomEvent Event { get; init; }
    public IWiredPolicy Policy { get; } = new WiredPolicy();

    public required IWiredStack Stack { get; init; }
    public IWiredTrigger? Trigger { get; init; } = null;
    public Dictionary<string, object?> Variables { get; } = [];

    public IWiredSelectionSet Selected { get; } = new WiredSelectionSet();
    public IWiredSelectionSet SelectorPool { get; } = new WiredSelectionSet();

    private readonly Dictionary<int, WiredSelectionSet> _wiredSelectionCache = [];

    public async Task<IWiredSelectionSet> GetWiredSelectionSetAsync(
        IWiredItem wired,
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
                                    var snapshot = await Room.GetFloorItemSnapshotByIdAsync(id, ct);

                                    if (snapshot is null)
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
                // Player sources not yet implemented
            }
        }

        _wiredSelectionCache[wired.Id] = set;

        return set;
    }

    public async Task<IWiredSelectionSet> GetEffectiveSelectionAsync(
        IWiredItem wired,
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
                        set.SelectedFurniIds.UnionWith(Selected.SelectedFurniIds);
                        break;
                }
            }
        }

        foreach (var source in wired.GetPlayerSources())
        {
            foreach (var sourceType in source)
            {
                // Player sources not yet implemented
            }
        }

        return result;
    }
}
