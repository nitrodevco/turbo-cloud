using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct) =>
        _furniModule.GetAllOwnersAsync(ct);
}
