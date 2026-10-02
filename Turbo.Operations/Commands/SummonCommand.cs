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

public sealed record SummonArguments(PlayerTarget Who);

/// <summary>
/// <c>:summon name</c>. Brings a player to the room the line was typed in. Intrusive, so it has a
/// node of its own apart from <c>:follow</c>, and it goes through the room's door like any entry.
/// </summary>
[Command(
    "summon",
    Description = "Bring a player to this room",
    Category = CommandCategories.SUPPORT
)]
[RequiresPermission(PermissionNodes.Command.SUMMON)]
public sealed class SummonCommand(IGrainFactory grainFactory, ISessionGateway sessionGateway)
    : IOperatorCommand<SummonArguments>
{
    private const string SUMMONED = "summoned";
    private const string NOT_ONLINE = "not_online";
    private const string SELF = "self";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [SUMMONED] = "%0% is on the way.",
            [NOT_ONLINE] = "%0% is not online.",
            [SELF] = "You are already here.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        SummonArguments arguments,
        CancellationToken ct
    )
    {
        if (ctx.Executor.RoomId is not { } roomId)
            return CommandResult.Fail(CommandReplyKeys.NEEDS_ROOM);

        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        if (target.Id == ctx.Executor.PlayerId)
            return CommandResult.Fail(SELF);

        if (!sessionGateway.GetOnlinePlayerIds().Contains(target.Id))
            return CommandResult.Fail(NOT_ONLINE, target.Name);

        await grainFactory.ForwardPlayerToRoomAsync(target.Id, roomId, ct);

        await ctx.NotifyAsync(
            target.Id,
            "command.summon.notice",
            "A moderator has requested that you join room %0%.",
            [roomId.ToString()],
            ct
        );

        return CommandResult.Done(SUMMONED, target.Name);
    }
}
