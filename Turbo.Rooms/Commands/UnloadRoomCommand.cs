using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:unloadroom</c>. For when a room's rows were changed by hand, or the room is stuck: everyone
/// is sent out, the owner and the executor too, and the room unloads, so the next visitor finds it
/// read from the database again. The executor receives the reply through player presence after
/// leaving the room.
/// </summary>
[Command(
    "unloadroom",
    Description = "Send everyone out and reload this room from the database",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.UNLOADROOM)]
public sealed class UnloadRoomCommand : ICommand<NoArguments>
{
    private const string UNLOADED = "unloaded";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [UNLOADED] = "The room was unloaded." };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    )
    {
        await ctx.Room.UnloadAsync(ct);

        return CommandResult.Done(UNLOADED);
    }
}
