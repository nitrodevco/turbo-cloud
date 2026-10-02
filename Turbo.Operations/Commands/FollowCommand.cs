using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record FollowArguments(PlayerTarget Who);

/// <summary>
/// <c>:follow name</c>. Takes the executor to the room a player is in, through the same entry a
/// click on the navigator makes: a locked door, a full room and a ban still apply to whoever
/// is not allowed past them.
/// </summary>
[Command(
    "follow",
    Description = "Go to the room a player is in",
    Category = CommandCategories.SUPPORT
)]
[RequiresPermission(PermissionNodes.Command.FOLLOW)]
public sealed class FollowCommand(IGrainFactory grainFactory, ISessionGateway sessionGateway)
    : IOperatorCommand<FollowArguments>
{
    private const string FOLLOWING = "following";
    private const string NOT_ONLINE = "not_online";
    private const string NOT_IN_ROOM = "not_in_room";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [FOLLOWING] = "Following %0% to room %1%.",
            [NOT_ONLINE] = "%0% is not online.",
            [NOT_IN_ROOM] = "%0% is not in a room.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        FollowArguments arguments,
        CancellationToken ct
    )
    {
        // The console has nobody to take anywhere.
        if (ctx.Executor.PlayerId is not { } executorId)
            return CommandResult.Fail(CommandReplyKeys.NEEDS_ROOM);

        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        if (!sessionGateway.GetOnlinePlayerIds().Contains(target.Id))
            return CommandResult.Fail(NOT_ONLINE, target.Name);

        var room = await grainFactory.GetPlayerPresenceGrain(target.Id).GetActiveRoomAsync(ct);

        if (room.RoomId.Value <= 0)
            return CommandResult.Fail(NOT_IN_ROOM, target.Name);

        await grainFactory.ForwardPlayerToRoomAsync(executorId, room.RoomId, ct);

        return CommandResult.Done(FOLLOWING, target.Name, room.RoomId.ToString());
    }
}
