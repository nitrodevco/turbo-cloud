using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Commands;

/// <summary>Who ran a command, where, and with what.</summary>
public interface ICommandContext
{
    /// <summary>The executor's action context, for calls that act on their behalf.</summary>
    ActionContext Action { get; }

    /// <summary>The player who typed the command.</summary>
    IRoomPlayer Executor { get; }

    RoomId RoomId { get; }

    ICommandRoom Room { get; }

    /// <summary>The line as typed after the name, unfiltered.</summary>
    string ArgumentText { get; }
}
