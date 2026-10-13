using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:pickall</c>. The Flash client asks "Do you really want to pick all furniture in the room?"
/// (<c>room.confirm.pick_all</c>) of the owner, a controller of any room or anyone with rights,
/// and only then sends the line as chat (<c>SessionDataManager.pickAllFurniture</c>). Every furni
/// goes where the infostand's pick up or eject would send it one at a time: the executor's own to
/// their inventory, everybody else's to their inventory with <c>room.furni.steal</c>, otherwise
/// back to its owners for whoever may eject (the room owner, a group admin, staff); a player who
/// may not eject takes only their own. Borrowed furni is <c>:pickallbc</c>'s. Nothing is said: the
/// furni leaving the room is the answer.
/// </summary>
[Command(
    "pickall",
    Description = "Pick up all the furni in the room; other people's goes back to them",
    Category = CommandCategories.GENERAL
)]
[RequiresPermission(PermissionNodes.Command.PICKALL)]
public sealed class PickAllCommand : ICommand<NoArguments>
{
    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    )
    {
        await ctx.Room.PickUpAllAsync(ct);

        return CommandResult.Ok;
    }
}
