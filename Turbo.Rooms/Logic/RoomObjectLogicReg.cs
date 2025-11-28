using System;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Logic;

internal sealed record RoomObjectLogicReg(
    IServiceProvider ServiceProvider,
    Func<IServiceProvider, IRoomObjectContext, IRoomObjectLogic> Factory
);
