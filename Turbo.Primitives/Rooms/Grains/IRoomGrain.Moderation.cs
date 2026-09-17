using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> MutePlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        int durationMinutes,
        CancellationToken ct
    );
    public Task<bool> UnmutePlayerAsync(ActionContext ctx, PlayerId playerId, CancellationToken ct);
    public Task<bool> ToggleRoomMuteAsync(ActionContext ctx, CancellationToken ct);
    public Task<bool> GetIsRoomMutedAsync(CancellationToken ct);
}
