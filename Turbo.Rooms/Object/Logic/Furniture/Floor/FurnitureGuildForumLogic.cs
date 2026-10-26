using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The group forum terminal. It is a customised guild furni on the server - same five stuff data
/// slots, same badge, same colours - and differs only in the client: a click opens no group
/// context menu, and using it opens the group's forum through the internal link
/// <c>groupforum/&lt;group id&gt;</c> it makes from the group id (FurnitureGroupForumTerminalLogic.as).
/// </summary>
[RoomObjectLogic(GuildFurnitureLogicNames.FORUM)]
public class FurnitureGuildForumLogic(
    IStuffDataFactory stuffDataFactory,
    IGrainFactory grainFactory,
    IRoomFloorItemContext ctx
) : FurnitureGuildCustomizedLogic(stuffDataFactory, grainFactory, ctx);
