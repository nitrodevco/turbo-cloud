using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

/// <summary>
/// <c>:ejectpets</c>. The Flash client sends the line as chat, without a confirm, for the room's
/// owner or a controller of every room (<c>SessionDataManager.ejectPets</c>). Every pet that is
/// not theirs goes back to its owner, as picking each up would. Nothing is said when it is
/// done: the pets leaving is the answer, as with <c>:ejectall</c>.
/// </summary>
[Command(
    "ejectpets",
    Description = "Send everybody else's pets in the room back to their owners",
    Category = CommandCategories.GENERAL
)]
[RequiresPermission(PermissionNodes.Command.EJECTPETS)]
public sealed class EjectPetsCommand : ICommand<NoArguments>
{
    private const string REFUSED = "refused";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [REFUSED] = "Only the room's owner can send other people's pets home.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => await ctx.Room.EjectPetsAsync(ct) is null ? CommandResult.Fail(REFUSED) : CommandResult.Ok;
}
