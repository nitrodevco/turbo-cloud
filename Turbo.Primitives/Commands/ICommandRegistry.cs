using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Commands;

/// <summary>An immutable view of the loaded commands.</summary>
public interface ICommandRegistry
{
    IReadOnlyCollection<CommandDescriptor> Commands { get; }

    /// <summary>
    /// Finds a command by name or alias without allocating, ignoring case. This is on the path of
    /// every chat line that starts with a colon.
    /// </summary>
    bool TryFind(ReadOnlySpan<char> name, out CommandDescriptor descriptor);

    /// <summary>Every other name a command answers to: its declared aliases and the hotel's.</summary>
    IReadOnlyList<string> AliasesOf(CommandDescriptor descriptor);
}
