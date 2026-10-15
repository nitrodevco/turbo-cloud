using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:resetscores</c>. The Flash client asks "Do you really want to reset the leaderboards in
/// this room?" (<c>room.confirm.resetscores</c>) of the room's owner, a controller of every room
/// or anyone with rights, and only then sends the line as chat
/// (<c>SessionDataManager.resetScores</c>). Every highscore board in the room is emptied. Nothing
/// is said when it is done: the emptied boards are the answer.
/// </summary>
[Command(
    "resetscores",
    Description = "Empty the room's leaderboards",
    Category = CommandCategories.GENERAL
)]
[RequiresPermission(PermissionNodes.Command.RESETSCORES)]
public sealed class ResetScoresCommand : ICommand<NoArguments>
{
    private const string REFUSED = "refused";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [REFUSED] = "You need rights in this room to reset its leaderboards.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) =>
        await ctx.Room.ResetScoresAsync(ct) is null
            ? CommandResult.Fail(REFUSED)
            : CommandResult.Ok;
}
