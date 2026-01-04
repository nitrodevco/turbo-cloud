using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredContext
{
    public IRoomGrain Room { get; }
    public RoomEvent Event { get; }
    public IWiredPolicy Policy { get; }

    public IWiredStack Stack { get; }
    public IWiredTrigger? Trigger { get; }
    public Dictionary<string, object?> Variables { get; }

    public IWiredSelectionSet Selected { get; }
    public IWiredSelectionSet SelectorPool { get; }
    public Task<IWiredSelectionSet> GetWiredSelectionSetAsync(
        IWiredItem wired,
        CancellationToken ct
    );
    public Task<IWiredSelectionSet> GetEffectiveSelectionAsync(
        IWiredItem wired,
        CancellationToken ct
    );
    public WiredContextSnapshot GetSnapshot();
}
