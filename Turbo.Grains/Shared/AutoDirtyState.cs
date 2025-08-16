using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Orleans.Runtime;

/// <summary>
/// Manages a persistent state object and tracks changes automatically via property and collection notifications.
/// </summary>
public sealed class AutoDirtyState<T>
    where T : class
{
    private readonly IPersistentState<T> _inner;
    private readonly Func<T>? _factory;

    private bool _isLoaded;
    private readonly HashSet<object> _hooked = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Initializes a new instance of <see cref="AutoDirtyState{T}"/>.
    /// </summary>
    public AutoDirtyState(IPersistentState<T> inner, Func<T>? factory = null)
    {
        _inner = inner;
        _factory = factory;
    }

    /// <summary>
    /// Gets a value indicating whether true after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Gets a value indicating whether true after any observed change since last accept/write.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Gets live state instance (after <see cref="InitializeAsync"/>).
    /// </summary>
    public T State => _inner.State!;

    /// <summary>
    /// Reads from storage once; creates via factory if null; hooks change events.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        await _inner.ReadStateAsync();

        if (_inner.State is null)
        {
            if (_factory is null)
            {
                throw new InvalidOperationException($"AutoDirtyState<{typeof(T).Name}> needs a factory when storage returns null.");
            }

            _inner.State = _factory();
        }

        Rehook();
        IsDirty = false;
        _isLoaded = true;
    }

    /// <summary>
    /// Applies a mutation and marks dirty.
    /// </summary>
    public void Mutate(Action<T> change)
    {
        EnsureLoaded();
        change(State);
        MarkDirty();
    }

    /// <summary>
    /// Replaces the root object (re-hooks) and marks dirty.
    /// </summary>
    public void Replace(T newState)
    {
        _inner.State = newState;
        Rehook();
        MarkDirty();
    }

    /// <summary>
    /// Persists only if dirty, then clears dirty flag.
    /// </summary>
    public async Task WriteIfDirtyAsync()
    {
        if (!_isLoaded || !IsDirty)
        {
            return;
        }

        await _inner.WriteStateAsync();
        AcceptChanges();
    }

    /// <summary>
    /// Persists regardless of dirty flag.
    /// </summary>
    public async Task WriteAsync()
    {
        EnsureLoaded();
        await _inner.WriteStateAsync();
        AcceptChanges();
    }

    /// <summary>
    /// Clears storage and local hooks; next call must re-initialize.
    /// </summary>
    public async Task ClearAsync()
    {
        await _inner.ClearStateAsync();
        UnhookObject(_inner.State);
        _hooked.Clear();
        IsDirty = false;
        _isLoaded = false;
    }

    /// <summary>
    /// Manually accepts current snapshot as clean.
    /// </summary>
    public void AcceptChanges() => IsDirty = false;

    /// <summary>
    /// Marks state as dirty (useful from external observers).
    /// </summary>
    public void MarkDirty() => IsDirty = true;

    // -------------------- Hooking logic --------------------
    private void Rehook()
    {
        _hooked.Clear();
        HookObject(_inner.State);
    }

    private void HookObject(object? obj)
    {
        if (obj is null)
        {
            return;
        }

        if (!_hooked.Add(obj))
        {
            return; // already hooked
        }

        if (obj is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnAnyPropertyChanged;
        }

        if (obj is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += OnAnyCollectionChanged;

            if (obj is IEnumerable seq)
            {
                foreach (var it in seq)
                {
                    HookObject(it);
                }
            }
        }

        // Walk simple properties to find nested INPC/INCC graphs
        foreach (var p in obj.GetType().GetProperties())
        {
            if (p.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? val = null;
            try
            {
                val = p.GetValue(obj);
            }
            catch
            { /* ignore getters with side-effects */
            }

            if (val is null)
            {
                continue;
            }

            if (val is INotifyPropertyChanged || val is INotifyCollectionChanged || val is IEnumerable)
            {
                HookObject(val);
            }
        }
    }

    private void UnhookObject(object? obj)
    {
        if (obj is null)
        {
            return;
        }

        if (!_hooked.Remove(obj))
        {
            return;
        }

        if (obj is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged -= OnAnyPropertyChanged;
        }

        if (obj is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged -= OnAnyCollectionChanged;
        }
    }

    private void OnAnyPropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    private void OnAnyCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MarkDirty();
        if (e.NewItems is { Count: > 0 })
        {
            foreach (var it in e.NewItems)
            {
                HookObject(it);
            }
        }

        // Usually no need to unhook removed items.
    }

    private void EnsureLoaded()
    {
        if (!_isLoaded)
        {
            throw new InvalidOperationException("Call InitializeAsync() first.");
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
