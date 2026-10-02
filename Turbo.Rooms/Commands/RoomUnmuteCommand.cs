using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:roomunmute</c>. Lets the room's visitors speak again after <see cref="RoomMuteCommand"/>
/// or the owner's own mute.
/// </summary>
[Command(
    "roomunmute",
    Description = "Let the room's visitors speak again",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.ROOMUNMUTE)]
public sealed class RoomUnmuteCommand : ICommand<NoArguments>
{
    private const string UNMUTED = "unmuted";
    private const string NOT_MUTED = "not_muted";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [UNMUTED] = "The room is no longer muted.",
            [NOT_MUTED] = "The room is not muted.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) =>
        await ctx.Room.SetRoomMutedAsync(false, ct)
            ? CommandResult.Done(UNMUTED)
            : CommandResult.Fail(NOT_MUTED);
}
