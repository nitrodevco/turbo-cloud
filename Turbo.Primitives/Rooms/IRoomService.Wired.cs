using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    /// <summary>
    /// The wired menu's Reload and Rollback (and <c>:reload</c> / <c>:rollback</c>): the room
    /// writes what it keeps and lets everyone go; each of them is told they are not playing a
    /// game and has their room session closed, then is forwarded back in after
    /// <c>RoomConfig.RoomStateReturnDelayMs</c>, as Habbo does.
    /// </summary>
    public Task ReloadWiredRoomAsync(ActionContext ctx, bool rollback, CancellationToken ct);
}
