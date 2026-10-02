using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:roommute</c>. Silences the room's visitors, so an event can be run without talk over it.
/// The owner, anyone with rights and staff can still speak, as with the owner's own toggle.
/// </summary>
[Command(
    "roommute",
    Description = "Silence the room's visitors",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.ROOMMUTE)]
public sealed class RoomMuteCommand : ICommand<NoArguments>
{
    private const string MUTED = "muted";
    private const string ALREADY = "already";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [MUTED] = "The room is muted.",
            [ALREADY] = "The room is already muted.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) =>
        await ctx.Room.SetRoomMutedAsync(true, ct)
            ? CommandResult.Done(MUTED)
            : CommandResult.Fail(ALREADY);
}
