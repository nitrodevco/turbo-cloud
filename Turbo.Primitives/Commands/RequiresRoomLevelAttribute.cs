using System;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Commands;

/// <summary>
/// The lowest controller level the executor needs in the room they typed the command in, checked
/// by core alongside the command's permission node. A command with it needs a room.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresRoomLevelAttribute(RoomControllerType level) : Attribute
{
    public RoomControllerType Level { get; } = level;
}
