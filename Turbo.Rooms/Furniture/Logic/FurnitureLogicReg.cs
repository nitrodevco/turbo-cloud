using System;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Logic;

namespace Turbo.Rooms.Furniture.Logic;

internal sealed record FurnitureLogicReg(
    IServiceProvider ServiceProvider,
    Func<IServiceProvider, IRoomItemContext, IFurnitureLogic> Factory
);
