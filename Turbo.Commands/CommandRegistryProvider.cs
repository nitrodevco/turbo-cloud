using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Commands;

namespace Turbo.Commands;

/// <summary>
/// The live command registry. A registration is checked whole against what is loaded and against
/// itself before anything changes, so a plugin whose command clashes fails its load with nothing
/// registered, as the permission registry does. Plugins register in parallel, so "first wins"
/// would depend on timing.
/// </summary>
public sealed class CommandRegistryProvider(
    ILogger<ICommandRegistryProvider> logger,
    CommandArgumentParserRegistry? parsers = null
) : ICommandRegistryProvider
{
    private readonly ILogger<ICommandRegistryProvider> _logger = logger;
    private readonly Lock _gate = new();
    private readonly List<Registration> _registrations = [];

    private IReadOnlyDictionary<string, IReadOnlyList<string>> _hotelAliases =
        new Dictionary<string, IReadOnlyList<string>>();

    private volatile CommandRegistry _current = CommandRegistry.Empty;

    public ICommandRegistry Current => _current;

    public event Action? Changed;

    public IDisposable Register(IEnumerable<ICommand> commands)
    {
        var batch = commands
            .Select(command => CommandDescriptorFactory.Create(command, parsers))
            .ToList();
        var registration = new Registration(batch);

        lock (_gate)
        {
            var taken = NamesTaken();

            foreach (var descriptor in batch)
            foreach (var name in descriptor.Aliases.Prepend(descriptor.Name))
                if (!taken.TryAdd(name, descriptor.Type.Name))
                    throw new InvalidOperationException(
                        $"Command name '{name}' of {descriptor.Type.Name} is already taken by {taken[name]}."
                    );

            foreach (
                var descriptor in batch.Where(x => ClientSwallowedCommands.Names.Contains(x.Name))
            )
                _logger.LogWarning(
                    "Command '{Command}' is a name the AIR client runs itself, so players on it cannot reach this command",
                    descriptor.Name
                );

            _registrations.Add(registration);
            Rebuild();
        }

        Changed?.Invoke();

        return new Removal(this, registration);
    }

    public void SetHotelAliases(IReadOnlyDictionary<string, IReadOnlyList<string>> aliasesByCommand)
    {
        lock (_gate)
        {
            _hotelAliases = aliasesByCommand;
            Rebuild();
        }

        Changed?.Invoke();
    }

    private void Remove(Registration registration)
    {
        lock (_gate)
        {
            if (!_registrations.Remove(registration))
                return;

            Rebuild();
        }

        Changed?.Invoke();
    }

    private Dictionary<string, string> NamesTaken()
    {
        var taken = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in _registrations.SelectMany(x => x.Descriptors))
        foreach (var name in descriptor.Aliases.Prepend(descriptor.Name))
            taken[name] = descriptor.Type.Name;

        return taken;
    }

    /// <summary>Builds the snapshot; callers hold the gate.</summary>
    private void Rebuild()
    {
        var byName = new Dictionary<string, CommandDescriptor>(StringComparer.OrdinalIgnoreCase);
        var all = _registrations.SelectMany(x => x.Descriptors).ToList();

        // Declared names first: they always keep working.
        foreach (var descriptor in all)
        foreach (var name in descriptor.Aliases.Prepend(descriptor.Name))
            byName[name] = descriptor;

        foreach (var descriptor in all)
        {
            if (!_hotelAliases.TryGetValue(descriptor.Name, out var added))
                continue;

            foreach (var alias in added)
                if (!byName.TryAdd(alias, descriptor))
                    _logger.LogWarning(
                        "Hotel alias '{Alias}' for command '{Command}' clashes with another command and is ignored",
                        alias,
                        descriptor.Name
                    );
        }

        _current = new CommandRegistry(all, byName);
    }

    private sealed class Registration(List<CommandDescriptor> descriptors)
    {
        public List<CommandDescriptor> Descriptors { get; } = descriptors;
    }

    private sealed class Removal(CommandRegistryProvider provider, Registration registration)
        : IDisposable
    {
        public void Dispose() => provider.Remove(registration);
    }
}
