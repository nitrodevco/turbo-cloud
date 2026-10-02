using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Commands;

public sealed record KickArguments(IRoomPlayer Target);

/// <summary>
/// <c>:kick name</c>. Everyone holds it by default because the room's own rules already decide:
/// the owner and those with rights may kick as the room's settings allow, and an owner, and
/// staff above the kicker, are protected. Both clients send the line as chat whatever the
/// player's rank, so a hotel that wants it staff-only denies the node.
/// </summary>
[Command(
    "kick",
    Description = "Kick a player from the room",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.KICK)]
public sealed class KickCommand : ICommand<KickArguments>
{
    private const string KICKED = "kicked";
    private const string REFUSED = "refused";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [KICKED] = "%0% was kicked from the room.",
            [REFUSED] = "You can't kick %0%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        KickArguments arguments,
        CancellationToken ct
    ) =>
        await ctx.Room.KickAsync(arguments.Target, ct)
            ? CommandResult.Done(KICKED, arguments.Target.Name)
            : CommandResult.Fail(REFUSED, arguments.Target.Name);
}
