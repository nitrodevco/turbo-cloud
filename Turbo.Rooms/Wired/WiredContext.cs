using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object.Avatars;
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
    public IWiredSelectionSet Signal { get; init; } = new WiredSelectionSet();
    public int Depth { get; init; }
    public Dictionary<string, int> Variables { get; init; } = [];
    public CancellationToken CancellationToken { get; init; }

    public bool TryGetContextVariable(string key, out int value)
    {
        if (Variables.TryGetValue(key, out var intValue))
        {
            value = intValue;

            return true;
        }

        value = default;

        return false;
    }

    public Task<bool> SetContextVariableAsync(string key, int value)
    {
        Variables[key] = value;

        return Task.FromResult(true);
    }

    /// <summary>
    /// Resolves every input slot of a box against this firing: the triggering furni and users,
    /// the box's own picks, the selector pool, the forwarded signal payload or the whole room.
    /// </summary>
    public IWiredSelectionSet GetSelection(IWiredBox wired)
    {
        var set = new WiredSelectionSet();

        foreach (var source in wired.GetFurniSources())
        {
            foreach (var sourceType in source)
            {
                switch (sourceType)
                {
                    case WiredFurniSourceType.TriggeredItem:
                        set.SelectedFurniIds.UnionWith(Selected.SelectedFurniIds);
                        break;
                    case WiredFurniSourceType.SelectedItems:
                    case WiredFurniSourceType.SnapshotItems:
                        AddExistingItems(set, wired.GetStuffIds());
                        AddExistingItems(set, wired.GetStuffIds2());
                        break;
                    case WiredFurniSourceType.SelectorItems:
                        set.SelectedFurniIds.UnionWith(SelectorPool.SelectedFurniIds);
                        break;
                    case WiredFurniSourceType.SignalItems:
                        set.SelectedFurniIds.UnionWith(Signal.SelectedFurniIds);
                        break;
                    case WiredFurniSourceType.AllRoomItems:
                        foreach (var item in _roomGrain.FurniModule.Items)
                            set.SelectedFurniIds.Add(item.ObjectId);
                        break;
                }
            }
        }

        foreach (var source in wired.GetPlayerSources())
        {
            foreach (var sourceType in source)
            {
                switch (sourceType)
                {
                    case WiredPlayerSourceType.TriggeredUser:
                    case WiredPlayerSourceType.ReachedUser:
                    case WiredPlayerSourceType.ClickedUser:
                        set.SelectedAvatarIds.UnionWith(Selected.SelectedAvatarIds);
                        break;
                    case WiredPlayerSourceType.SelectorUsers:
                        set.SelectedAvatarIds.UnionWith(SelectorPool.SelectedAvatarIds);
                        break;
                    case WiredPlayerSourceType.SignalUsers:
                        set.SelectedAvatarIds.UnionWith(Signal.SelectedAvatarIds);
                        break;
                    case WiredPlayerSourceType.UserByName:
                        AddPlayersByName(set, wired.GetSnapshot().StringParam);
                        break;
                    case WiredPlayerSourceType.AllRoomUsers:
                        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
                            set.SelectedAvatarIds.Add(avatar.ObjectId);
                        break;
                    case WiredPlayerSourceType.BotByName:
                        // Bots are not players; bot actions resolve the named bot themselves.
                        break;
                }
            }
        }

        return set;
    }

    public virtual WiredContextSnapshot GetSnapshot() =>
        new()
        {
            Variables = new Dictionary<string, int>(Variables),
            Selected = Selected.GetSnapshot(),
        };

    private void AddExistingItems(WiredSelectionSet set, List<int>? stuffIds)
    {
        if (stuffIds is null)
            return;

        foreach (var id in stuffIds)
        {
            if (_roomGrain.FurniModule.HasItem(id))
                set.SelectedFurniIds.Add(id);
        }
    }

    private void AddPlayersByName(WiredSelectionSet set, string names)
    {
        if (string.IsNullOrWhiteSpace(names))
            return;

        var wanted = new HashSet<string>(
            names.Split(['\t', '\r', '\n', ','], System.StringSplitOptions.RemoveEmptyEntries),
            System.StringComparer.OrdinalIgnoreCase
        );

        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
        {
            if (wanted.Contains(avatar.Name.Trim()))
                set.SelectedAvatarIds.Add(avatar.ObjectId);
        }
    }
}
