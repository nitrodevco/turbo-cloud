using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The group forum terminal. It is a customised guild furni in every way that matters — same
/// five stuff data slots, same badge, same colours, same context menu — and differs only in
/// which entries the client draws in that menu, which it decides from the furni's own class
/// name rather than from anything the server sends.
/// </summary>
[RoomObjectLogic(GuildFurnitureLogicNames.FORUM)]
public class FurnitureGuildForumLogic(
    IStuffDataFactory stuffDataFactory,
    IGrainFactory grainFactory,
    IRoomFloorItemContext ctx
) : FurnitureGuildCustomizedLogic(stuffDataFactory, grainFactory, ctx);
