using System;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

internal sealed record WiredDefinitionReg(
    IServiceProvider ServiceProvider,
    Func<IServiceProvider, IRoomFloorItemContext, IWiredDefinition> Factory
);
