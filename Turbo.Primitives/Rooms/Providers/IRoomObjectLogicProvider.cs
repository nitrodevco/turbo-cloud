using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomObjectLogicProvider
{
    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomObjectContext, IRoomObjectLogic> factory
    );
    public IRoomObjectLogic CreateLogicInstance(string logicType, IRoomObjectContext ctx);

    /// <summary>Every logic type registered now, plugins' included, by name.</summary>
    public IReadOnlyList<string> LogicTypes { get; }

    /// <summary>
    /// Moves on whenever a logic type is registered or taken away (a plugin loading, reloading or
    /// unloading): a loaded room then rebinds its furni whose logic is no longer current.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Whether <paramref name="logic"/> is still what its type makes: false once the registration
    /// that made it is gone or replaced (a plugin unloaded or reloaded), and for a fallback made
    /// while its type had none, once it has one.
    /// </summary>
    public bool IsCurrent(IRoomObjectLogic logic);
}
