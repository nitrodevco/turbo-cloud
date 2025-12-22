using System;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

internal sealed record WiredDefinitionReg(
    IServiceProvider ServiceProvider,
    Func<IServiceProvider, IRoomObjectContext, IWiredDefinition> Factory
);
