using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Commands;

public sealed record RoomEffectArguments(SpecialRoomEffectType Effect);

/// <summary>
/// <c>:roomeffect rotate|shake|flip|disco</c>. Plays one of the client's room-wide effects for
/// everyone in the room (<c>SpecialRoomEffect</c>): a few seconds of spectacle for an event, with
/// nothing kept, so whoever walks in afterwards sees an ordinary room.
/// </summary>
[Command(
    "roomeffect",
    Description = "Rotate, shake, flip or light up the room for everyone in it",
    Category = CommandCategories.ANNOUNCEMENTS
)]
[RequiresPermission(PermissionNodes.Command.ROOMEFFECT)]
public sealed class RoomEffectCommand : ICommand<RoomEffectArguments>
{
    private const string PLAYED = "played";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [PLAYED] = "Playing %0% for the room." };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        RoomEffectArguments arguments,
        CancellationToken ct
    )
    {
        await ctx.Room.PlayEffectAsync(arguments.Effect, ct);

        return CommandResult.Done(PLAYED, arguments.Effect.ToString().ToLowerInvariant());
    }
}
