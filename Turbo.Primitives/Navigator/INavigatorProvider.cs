using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Navigator;

public interface INavigatorProvider
{
    public Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextsAsync();
    public Task<List<RoomInfoSnapshot>> GetRoomResultsAsync(CancellationToken ct = default);
    public Task ReloadAsync(CancellationToken ct = default);
}
