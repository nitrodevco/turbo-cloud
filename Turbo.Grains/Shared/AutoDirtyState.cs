using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using Orleans.Runtime;

namespace Turbo.Grains.Shared;

public sealed class AutoDirtyState<T> where T : class
{
    private readonly IPersistentState<T> _inner;
    public T State => _inner.State;
    public bool IsDirty { get; private set; }

    public AutoDirtyState(IPersistentState<T> inner)
    {
        _inner = inner;
        HookObject(State);
    }

    // Call after you persist external systems to accept current snapshot as clean
    public void AcceptChanges() => IsDirty = false;

    public Task WriteAsync() => _inner.WriteStateAsync();
    public Task ClearAsync() => _inner.ClearStateAsync();

    private void MarkDirty() => IsDirty = true;

    private void HookObject(object? obj)
    {
        if (obj is null) return;

        if (obj is INotifyPropertyChanged npc)
            npc.PropertyChanged += (_, __) => MarkDirty();

        if (obj is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += (_, e) =>
            {
                MarkDirty();
                if (e.NewItems != null) foreach (var it in e.NewItems) HookObject(it);
                // You could unhook old items on Remove if they implement INPC—usually not necessary
            };

            // Hook existing items
            var asEnumerable = (obj as System.Collections.IEnumerable)!;
            foreach (var it in asEnumerable) HookObject(it);
        }

        // Optionally walk simple properties to hook nested INPC/INCC objects
        foreach (var p in obj.GetType().GetProperties())
        {
            if (p.GetIndexParameters().Length > 0) continue;
            var val = p.GetValue(obj);
            if (val is INotifyPropertyChanged or INotifyCollectionChanged) HookObject(val);
        }
    }
}