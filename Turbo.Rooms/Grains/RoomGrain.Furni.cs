using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<ImmutableDictionary<long, string>> GetAllOwnersAsync(CancellationToken ct) =>
        _furniModule.GetAllOwnersAsync(ct);
}
