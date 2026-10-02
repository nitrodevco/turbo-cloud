using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Commands;

/// <summary>
/// The commands that are loaded: core's, and those of every plugin that is. A load or unload
/// replaces <see cref="Current"/> with a new snapshot, so a reader never sees half a plugin.
/// </summary>
public interface ICommandRegistryProvider
{
    ICommandRegistry Current { get; }

    /// <summary>Raised after <see cref="Current"/> is replaced.</summary>
    event System.Action? Changed;

    /// <summary>
    /// Adds a plugin's commands. Disposing the result takes them out again.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A command is malformed or claims a name or alias that is taken; nothing is added.
    /// </exception>
    IDisposable Register(IEnumerable<ICommand> commands);

    /// <summary>
    /// Sets the aliases a hotel's texts add, by command name. An alias that clashes is reported
    /// and ignored, and the declared names always keep working.
    /// </summary>
    void SetHotelAliases(IReadOnlyDictionary<string, IReadOnlyList<string>> aliasesByCommand);
}
