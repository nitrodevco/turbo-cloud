using System;
using System.Threading.Tasks;
using Orleans.Runtime;

namespace Turbo.Grains.Shared;

/// <summary>
/// Provides a wrapper for managing grain state with automatic dirty tracking and persistence.
/// </summary>
public sealed class GrainStateHost<TState>(IPersistentState<TState> inner, Func<TState> factory)
    where TState : class
{
    private readonly AutoDirtyState<TState> _auto = new(inner, factory);
    private readonly Func<TState> _factory = factory;

    /// <summary>
    /// Gets the current state object.
    /// </summary>
    public TState State => _auto.State;

    /// <summary>
    /// Gets a value indicating whether gets whether the state is dirty (has changes since last write).
    /// </summary>
    public bool IsDirty => _auto.IsDirty;

    /// <summary>
    /// Initializes the state from persistent storage.
    /// </summary>
    public Task InitializeAsync() => _auto.InitializeAsync();

    /// <summary>
    /// Writes the state to persistent storage if dirty.
    /// </summary>
    public Task WriteIfDirtyAsync() => _auto.WriteIfDirtyAsync();

    /// <summary>
    /// Accepts the current state as clean (not dirty).
    /// </summary>
    public void AcceptChanges() => _auto.AcceptChanges();

    /// <summary>
    /// Replaces the current state object and marks as dirty.
    /// </summary>
    public void Replace(TState newState) => _auto.Replace(newState);
}
