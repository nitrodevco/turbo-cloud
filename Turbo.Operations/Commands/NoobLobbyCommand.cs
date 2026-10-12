using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;

namespace Turbo.Operations.Commands;

[CommandBranch("add", typeof(NoobLobbyAddArguments))]
[CommandBranch("remove", typeof(NoobLobbyRemoveArguments))]
[CommandBranch("list", typeof(NoobLobbyListArguments))]
public abstract record NoobLobbyArguments;

public sealed record NoobLobbyAddArguments : NoobLobbyArguments;

public sealed record NoobLobbyRemoveArguments : NoobLobbyArguments;

public sealed record NoobLobbyListArguments : NoobLobbyArguments;

/// <summary>
/// <c>:nooblobby add|remove|list</c>: the hotel's new user lobbies, Habbo's "Enter a room to meet
/// other new users". <c>add</c> makes the room you are in one (door mode 4): only new users, staff
/// and whoever has rights get in, and new users are offered it from their home room and sent to it
/// by the toolbar. <c>remove</c> opens it to everyone again; <c>list</c> shows the lobbies and who
/// is in them.
/// </summary>
[Command(
    "nooblobby",
    Description = "Make this room a lobby for new users, or an ordinary room again",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.NOOBLOBBY)]
public sealed class NoobLobbyCommand(INoobLobbyDirectory lobbies)
    : IOperatorCommand<NoobLobbyArguments>
{
    private const string ADDED = "added";
    private const string ALREADY_ADDED = "already_added";
    private const string REMOVED = "removed";
    private const string NOT_A_LOBBY = "not_a_lobby";
    private const string LIST_EMPTY = "list_empty";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [ADDED] =
                "This room is now a lobby for new users: they are offered it and only they (and staff) get in.",
            [ALREADY_ADDED] = "This room is already a lobby for new users.",
            [REMOVED] = "This room is no longer a lobby for new users; its door is open.",
            [NOT_A_LOBBY] = "This room is not a lobby for new users. :nooblobby add makes it one.",
            [LIST_EMPTY] = "No lobbies for new users: they are not offered one.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        NoobLobbyArguments arguments,
        CancellationToken ct
    )
    {
        if (arguments is NoobLobbyListArguments)
            return await ListAsync(ctx, ct);

        if (ctx.Executor.RoomId is not { } roomId || ctx.Executor.PlayerId is not { } playerId)
            return CommandResult.Fail(CommandReplyKeys.NEEDS_ROOM);

        var isLobby = arguments is NoobLobbyAddArguments;
        var change = await lobbies.SetAsync(new RoomId(roomId.Value), isLobby, playerId, ct);

        return change switch
        {
            NoobLobbyChange.Added => CommandResult.Done(ADDED),
            NoobLobbyChange.AlreadyAdded => CommandResult.Done(ALREADY_ADDED),
            NoobLobbyChange.Removed => CommandResult.Done(REMOVED),
            _ => CommandResult.Fail(NOT_A_LOBBY),
        };
    }

    private async ValueTask<CommandResult> ListAsync(
        IOperatorCommandContext ctx,
        CancellationToken ct
    )
    {
        var rooms = await lobbies.ListAsync(ct);

        if (rooms.IsEmpty)
            return CommandResult.Done(LIST_EMPTY);

        await ctx.Executor.NoticeAsync(
            [
                .. rooms.Select(room =>
                    $"{room.Name} (room {room.RoomId.Value}) - {room.Players}/{room.PlayersMax}"
                ),
            ],
            ct
        );

        return CommandResult.Ok;
    }
}
