using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:mutebots</c>, "Ignore bot chats" (habbopages/chat/commands). The Flash client has no case
/// for it (<c>ChatInputWidgetHandler</c> special-cases <c>:mutepets</c> only), so the line arrives
/// as chat like any other. The player stops hearing bots talk in
/// the room, and saying it again lets them hear bots again (inference: Habbo's answer was not
/// recorded, so the player is told which it is now).
/// </summary>
[Command(
    "mutebots",
    Description = "Stop or start hearing bots talk",
    Category = CommandCategories.GENERAL
)]
[RequiresPermission(PermissionNodes.Command.MUTEBOTS)]
public sealed class MuteBotsCommand : ICommand<NoArguments>
{
    private const string MUTED = "muted";
    private const string UNMUTED = "unmuted";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [MUTED] = "You no longer hear bots talk in this room.",
            [UNMUTED] = "You hear bots talk again.",
        };

    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) =>
        ValueTask.FromResult(
            ctx.Room.ToggleBotsMuted() ? CommandResult.Done(MUTED) : CommandResult.Done(UNMUTED)
        );
}
