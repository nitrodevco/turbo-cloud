using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Commands;

namespace Turbo.Commands;

/// <summary>
/// An immutable snapshot of the loaded commands. Names are matched with a span lookup, so
/// asking about a line that is not a command, or a <c>:word</c> nobody registered, allocates
/// nothing: the check sits on the path of every chat line that starts with a colon.
/// </summary>
internal sealed class CommandRegistry : ICommandRegistry
{
    public static CommandRegistry Empty { get; } =
        new([], new Dictionary<string, CommandDescriptor>());

    private readonly FrozenDictionary<string, CommandDescriptor> _byName;
    private readonly FrozenDictionary<string, CommandDescriptor>.AlternateLookup<
        ReadOnlySpan<char>
    > _lookup;

    public CommandRegistry(
        IReadOnlyCollection<CommandDescriptor> commands,
        IReadOnlyDictionary<string, CommandDescriptor> byName
    )
    {
        Commands = commands.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        _byName = byName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _lookup = _byName.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public IReadOnlyCollection<CommandDescriptor> Commands { get; }

    public bool TryFind(ReadOnlySpan<char> name, out CommandDescriptor descriptor) =>
        _lookup.TryGetValue(name, out descriptor!);

    public IReadOnlyList<string> AliasesOf(CommandDescriptor descriptor) =>
        [
            .. _byName
                .Where(x =>
                    ReferenceEquals(x.Value, descriptor)
                    && !string.Equals(x.Key, descriptor.Name, StringComparison.OrdinalIgnoreCase)
                )
                .Select(x => x.Key)
                .Order(StringComparer.Ordinal),
        ];
}
