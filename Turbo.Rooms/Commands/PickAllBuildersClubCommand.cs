using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:pickallbc</c>. Sent as chat once the player has confirmed "You're about to remove all of
/// your Builders Club items from this room" (<c>room.confirm.pick_all_bc</c>,
/// <c>SessionDataManager.pickAllBuilderFurniture</c>). Every Builders Club furni the executor
/// placed in the room goes back to the club, as picking each up would. Nothing is said.
/// </summary>
[Command(
    "pickallbc",
    Description = "Give back all your Builders Club furni in the room",
    Category = CommandCategories.GENERAL
)]
[RequiresPermission(PermissionNodes.Command.PICKALLBC)]
public sealed class PickAllBuildersClubCommand : ICommand<NoArguments>
{
    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    )
    {
        await ctx.Room.PickUpAllOwnedAsync(true, ct);

        return CommandResult.Ok;
    }
}
