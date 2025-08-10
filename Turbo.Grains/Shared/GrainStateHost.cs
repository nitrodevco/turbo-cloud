using System;
using System.Threading.Tasks;
using Orleans.Runtime;

namespace Turbo.Grains.Shared;

public sealed class GrainStateHost<TState>(IPersistentState<TState> inner, Func<TState> factory) where TState : class
{
    private readonly AutoDirtyState<TState> _auto = new(inner, factory);
    private readonly Func<TState> _factory = factory;

    public TState State => _auto.State;
    public bool IsDirty => _auto.IsDirty;

    public Task InitializeAsync() => _auto.InitializeAsync();
    public Task WriteIfDirtyAsync() => _auto.WriteIfDirtyAsync();
    public void AcceptChanges() => _auto.AcceptChanges();

    public void Replace(TState newState) => _auto.Replace(newState);
}