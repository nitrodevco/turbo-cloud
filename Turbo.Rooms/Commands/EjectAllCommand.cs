using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:ejectall</c>. Sent as chat once the player has confirmed "Do you really want to eject all
/// Furni from each Habbo from the room?" (<c>room.confirm.eject_all</c>,
/// <c>SessionDataManager.ejectAllFurniture</c>). Only in a group room: Habbo's chat commands page
/// (habbopages/chat/commands) lists it as "Eject all furniture in a group room", and the client
/// asks only for rights, so the room is the server's to check. Everybody else's furni goes back to its owners,
/// on the terms of the infostand's eject (<c>InfoStandFurniView.updatePickupMode</c>): the room
/// owner, a group admin, or staff who may pick up any furni. The room owner's furni and the
/// executor's own stay. Nothing is said when it is done.
/// </summary>
[Command(
    "ejectall",
    Description = "Send everybody else's furni in the room back to its owners",
    Category = CommandCategories.GENERAL
)]
[RequiresPermission(PermissionNodes.Command.EJECTALL)]
public sealed class EjectAllCommand : ICommand<NoArguments>
{
    private const string REFUSED = "refused";
    private const string NOT_GROUP_ROOM = "not_group_room";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [REFUSED] = "You can't eject other people's furni in this room.",
            [NOT_GROUP_ROOM] = ":ejectall only works in a group room.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    )
    {
        if (!await ctx.Room.IsGroupRoomAsync(ct))
            return CommandResult.Fail(NOT_GROUP_ROOM);

        return await ctx.Room.EjectAllAsync(ct) is null
            ? CommandResult.Fail(REFUSED)
            : CommandResult.Ok;
    }
}
