using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<ImmutableDictionary<long, string>> GetAllOwnersAsync(CancellationToken ct);
}
