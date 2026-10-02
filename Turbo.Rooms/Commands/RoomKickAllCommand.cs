using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:roomkickall</c>. Clears the room for an event or an abusive crowd: everyone leaves but the
/// owner and anyone who may moderate any room. It needs no controller level in the room, because
/// the staff who use it are not the room's owner.
/// </summary>
[Command(
    "roomkickall",
    Description = "Clear the room of everyone but its owner and staff",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.ROOMKICKALL)]
public sealed class RoomKickAllCommand : ICommand<NoArguments>
{
    private const string CLEARED = "cleared";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [CLEARED] = "%0% visitors were sent out of the room." };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => CommandResult.Done(CLEARED, (await ctx.Room.ClearAsync(ct)).ToString());
}
