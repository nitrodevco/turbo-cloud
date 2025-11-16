using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Navigator;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Navigator;

public interface INavigatorProvider
{
    public Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextsAsync();
    public Task<List<RoomInfoSnapshot>> GetRoomResultsAsync(CancellationToken ct = default);
    public Task ReloadAsync(CancellationToken ct = default);
}
