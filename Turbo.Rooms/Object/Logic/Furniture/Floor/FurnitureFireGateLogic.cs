using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The Wall Of Flame (<c>wf_firegate</c>): the Glass Door's behaviour with the fire burning in
/// state 0 and out in state 1 (official client capture 2026-10-17).
/// </summary>
[RoomObjectLogic("wf_firegate")]
public class FurnitureFireGateLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureWiredDoorLogic(stuffDataFactory, ctx);
