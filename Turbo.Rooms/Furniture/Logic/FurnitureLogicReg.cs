using System;

namespace Turbo.Rooms.Furniture.Logic;

internal sealed record FurnitureLogicReg(
    IServiceProvider ServiceProvider,
    Func<IServiceProvider, object> Activator
);
