using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:pickall</c>. The Flash client asks "Do you really want to pick all furniture in the room?"
/// (<c>room.confirm.pick_all</c>) of the owner, a controller of any room or anyone with rights,
/// and only then sends the line as chat (<c>SessionDataManager.pickAllFurniture</c>). Every furni
/// the executor owns in the room goes back to their inventory, as picking each up would; other
/// people's furni is <c>:ejectall</c>'s, and borrowed furni <c>:pickallbc</c>'s. Nothing is
/// said: the furni leaving the room is the answer.
/// </summary>
[Command(
    "pickall",
    Description = "Pick up all your furni in the room",
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
        await ctx.Room.PickUpAllOwnedAsync(false, ct);

        return CommandResult.Ok;
    }
}
