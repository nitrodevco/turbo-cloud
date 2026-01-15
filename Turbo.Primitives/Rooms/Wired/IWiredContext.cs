using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredContext
{
    public Dictionary<string, object?> Variables { get; }
    public IWiredPolicy Policy { get; }
    public IWiredSelectionSet Selected { get; }
    public IWiredSelectionSet SelectorPool { get; }

    public Task<IWiredSelectionSet> GetWiredSelectionSetAsync(
        IWiredBox wired,
        CancellationToken ct
    );
    public Task<IWiredSelectionSet> GetEffectiveSelectionAsync(
        IWiredBox wired,
        CancellationToken ct
    );
    public WiredContextSnapshot GetSnapshot();
}
