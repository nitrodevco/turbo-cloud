using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:mutepets</c>, "Ignore pet chats" (habbopages/chat/commands). The Flash client sends the
/// line as chat for anyone (<c>ChatInputWidgetHandler</c>). The player stops hearing pets talk in
/// the room, and saying it again lets them hear pets again (inference: Habbo's answer was not
/// recorded, so the player is told which it is now).
/// </summary>
[Command(
    "mutepets",
    Description = "Stop or start hearing pets talk",
    Category = CommandCategories.GENERAL
)]
[RequiresPermission(PermissionNodes.Command.MUTEPETS)]
public sealed class MutePetsCommand : ICommand<NoArguments>
{
    private const string MUTED = "muted";
    private const string UNMUTED = "unmuted";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [MUTED] = "You no longer hear pets talk in this room.",
            [UNMUTED] = "You hear pets talk again.",
        };

    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) =>
        ValueTask.FromResult(
            ctx.Room.TogglePetsMuted() ? CommandResult.Done(MUTED) : CommandResult.Done(UNMUTED)
        );
}
