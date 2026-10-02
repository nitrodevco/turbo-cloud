using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Commands;

internal sealed record RoomCommandContext(
    ActionContext Action,
    IRoomPlayer Executor,
    RoomId RoomId,
    ICommandRoom Room,
    string ArgumentText
) : ICommandContext;
