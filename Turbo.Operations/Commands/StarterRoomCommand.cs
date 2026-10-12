using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;

namespace Turbo.Operations.Commands;

[CommandBranch("add", typeof(StarterRoomAddArguments))]
[CommandBranch("remove", typeof(StarterRoomRemoveArguments))]
[CommandBranch("picture", typeof(StarterRoomPictureArguments))]
[CommandBranch("list", typeof(StarterRoomListArguments))]
public abstract record StarterRoomArguments;

public sealed record StarterRoomAddArguments : StarterRoomArguments;

public sealed record StarterRoomRemoveArguments : StarterRoomArguments;

public sealed record StarterRoomPictureArguments : StarterRoomArguments;

public sealed record StarterRoomListArguments : StarterRoomArguments;

/// <summary>
/// <c>:starterroom add|remove|picture|list</c>: the hotel's starter rooms, from inside them. Build a
/// room, stand in it and <c>add</c> it: every new player who picks it in the new user flow gets a
/// copy of it as it is then, and the client's room picker offers it at once. <c>add</c> and
/// <c>picture</c> open the starter room camera (a client that speaks <c>starter.rooms</c>), whose
/// capture becomes the room's round picture in the picker. <c>remove</c> stops offering the room;
/// <c>list</c> shows what is offered, in the picker's order.
/// </summary>
[Command(
    "starterroom",
    Description = "Offer this room as a starter room for new players, or take its picture",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.STARTERROOM)]
public sealed class StarterRoomCommand(IStarterRoomDirectory directory, IGrainFactory grainFactory)
    : IOperatorCommand<StarterRoomArguments>
{
    private const string ADDED = "added";
    private const string ALREADY_ADDED = "already_added";
    private const string REMOVED = "removed";
    private const string NOT_A_STARTER_ROOM = "not_a_starter_room";
    private const string FULL = "full";
    private const string CAMERA = "camera";
    private const string LIST_EMPTY = "list_empty";
    private const string LIST_LINE = "list_line";
    private const string NO_PICTURE = "no_picture";
    private const string HAS_PICTURE = "has_picture";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [ADDED] =
                "%0% is now a starter room. Take its picture with the starter room camera; :starterroom picture opens it again.",
            [ALREADY_ADDED] =
                "%0% is already a starter room. :starterroom picture retakes its picture.",
            [REMOVED] = "%0% is no longer a starter room.",
            [NOT_A_STARTER_ROOM] = "This room is not a starter room. :starterroom add offers it.",
            [FULL] =
                "There are already %0% starter rooms, as many as the room picker shows. :starterroom remove one first.",
            [CAMERA] = "Frame the room in the circle and take its picture.",
            [LIST_EMPTY] = "No starter rooms: new players get the built-in ones.",
            [LIST_LINE] = "%0%. %1% (room %2%) - %3%",
            [NO_PICTURE] = "no picture yet",
            [HAS_PICTURE] = "picture taken",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        StarterRoomArguments arguments,
        CancellationToken ct
    )
    {
        if (arguments is StarterRoomListArguments)
            return await ListAsync(ctx, ct);

        if (ctx.Executor.RoomId is not { } roomId || ctx.Executor.PlayerId is not { } playerId)
            return CommandResult.Fail(CommandReplyKeys.NEEDS_ROOM);

        var room = new RoomId(roomId.Value);

        switch (arguments)
        {
            case StarterRoomAddArguments:
            {
                var change = await directory.AddAsync(room, playerId, ct);
                var name = (await directory.GetAsync(room, ct))?.RoomName ?? room.Value.ToString();

                if (change == StarterRoomChange.Full)
                    return CommandResult.Fail(
                        FULL,
                        directory.Capacity.ToString(
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                    );

                if (change == StarterRoomChange.AlreadyAdded)
                    return CommandResult.Done(ALREADY_ADDED, name);

                if (change != StarterRoomChange.Added)
                    return CommandResult.Fail(CommandReplyKeys.NEEDS_ROOM);

                await OpenCameraAsync(playerId, room, ct);

                return CommandResult.Done(ADDED, name);
            }

            case StarterRoomRemoveArguments:
            {
                var name = (await directory.GetAsync(room, ct))?.RoomName;

                return await directory.RemoveAsync(room, playerId, ct) == StarterRoomChange.Removed
                    ? CommandResult.Done(REMOVED, name ?? room.Value.ToString())
                    : CommandResult.Fail(NOT_A_STARTER_ROOM);
            }

            default:
            {
                if (await directory.GetAsync(room, ct) is null)
                    return CommandResult.Fail(NOT_A_STARTER_ROOM);

                await OpenCameraAsync(playerId, room, ct);

                return CommandResult.Done(CAMERA);
            }
        }
    }

    private async ValueTask<CommandResult> ListAsync(
        IOperatorCommandContext ctx,
        CancellationToken ct
    )
    {
        var rooms = await directory.ListAsync(ct);

        if (rooms.IsEmpty)
            return CommandResult.Done(LIST_EMPTY);

        await ctx.Executor.NoticeAsync(
            [
                .. rooms.Select(
                    (room, index) =>
                        $"{index + 1}. {room.RoomName} (room {room.RoomId.Value}) - "
                        + (
                            room.PictureTakenAt is null
                                ? DefaultTexts[NO_PICTURE]
                                : DefaultTexts[HAS_PICTURE]
                        )
                ),
            ],
            ct
        );

        return CommandResult.Ok;
    }

    /// <summary>The starter room camera, on a client that speaks <c>starter.rooms</c>; nothing on any other.</summary>
    private Task OpenCameraAsync(
        Turbo.Primitives.Players.PlayerId playerId,
        RoomId room,
        CancellationToken ct
    ) =>
        grainFactory.SendComposerToPlayerAsync(
            playerId,
            new TurboStarterRoomCameraMessage { RoomId = room.Value },
            ct
        );
}
