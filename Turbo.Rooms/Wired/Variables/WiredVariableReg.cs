using System;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Variables;

internal sealed record WiredVariableReg(
    IServiceProvider ServiceProvider,
    Func<IServiceProvider, IRoomGrain, IWiredVariable> Factory
);
