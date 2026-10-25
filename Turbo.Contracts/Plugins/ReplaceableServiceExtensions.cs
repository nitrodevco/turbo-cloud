using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Plugins;

namespace Turbo.Contracts.Plugins;

public static class ReplaceableServiceExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContract"/> through its slot, <typeparamref name="TDefault"/>
    /// the host's own: what the container gives for the contract forwards each call to the
    /// implementation in use, so a class built before a plugin replaced it calls the plugin's, and
    /// the host's again after it unloads (<c>docs/plugin-services.md</c>). The contract must be an
    /// interface marked <see cref="ReplaceableServiceAttribute"/>.
    /// </summary>
    public static IServiceCollection AddReplaceable<TContract, TDefault>(
        this IServiceCollection services
    )
        where TContract : class
        where TDefault : class, TContract
    {
        services.TryAddSingleton<TDefault>();

        return services.AddReplaceable<TContract>(sp => sp.GetRequiredService<TDefault>());
    }

    /// <inheritdoc cref="AddReplaceable{TContract, TDefault}(IServiceCollection)"/>
    public static IServiceCollection AddReplaceable<TContract>(
        this IServiceCollection services,
        Func<IServiceProvider, TContract> defaultFactory
    )
        where TContract : class
    {
        var contract = typeof(TContract);

        if (
            !contract.IsInterface || !contract.IsDefined(typeof(ReplaceableServiceAttribute), false)
        )
            throw new InvalidOperationException(
                $"{contract.Name} is registered as replaceable but is not an interface marked [ReplaceableService]."
            );

        services.TryAddSingleton<ReplaceableServices>();
        services.TryAddSingleton<IReplaceableServices>(sp =>
            sp.GetRequiredService<ReplaceableServices>()
        );
        services.AddSingleton(new ReplaceableContract(contract));
        services.AddSingleton<IServiceSlot<TContract>>(sp => new ServiceSlot<TContract>(
            defaultFactory(sp)
        ));
        services.AddSingleton<TContract>(sp =>
            ((ServiceSlot<TContract>)sp.GetRequiredService<IServiceSlot<TContract>>()).Forwarder
        );

        return services;
    }
}

/// <summary>A contract the host registered through its slot.</summary>
internal sealed record ReplaceableContract(Type Contract);

/// <summary>The slot side the registry drives, whatever the contract.</summary>
internal interface IServiceSlotEntries
{
    public IDisposable Push(string owner, object implementation, ILogger logger);

    public IEnumerable<ServiceReplacement> Entries { get; }
}

internal sealed class ReplaceableServices(
    IServiceProvider services,
    IEnumerable<ReplaceableContract> contracts,
    ILogger<ReplaceableServices>? logger = null
) : IReplaceableServices
{
    private readonly ImmutableHashSet<Type> _contracts = [.. contracts.Select(x => x.Contract)];
    private readonly ConcurrentDictionary<Type, IServiceSlotEntries> _replaced = new();
    private readonly ILogger _logger =
        (ILogger?)logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public bool IsReplaceable(Type contract) => _contracts.Contains(contract);

    public IDisposable Replace(Type contract, string owner, object implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        if (!IsReplaceable(contract))
            throw new ArgumentException(
                $"{contract.Name} is not a host service a plugin can replace.",
                nameof(contract)
            );

        if (!contract.IsInstanceOfType(implementation))
            throw new ArgumentException(
                $"{implementation.GetType().Name} does not implement {contract.Name}.",
                nameof(implementation)
            );

        var slot = _replaced.GetOrAdd(
            contract,
            type =>
                (IServiceSlotEntries)
                    services.GetRequiredService(typeof(IServiceSlot<>).MakeGenericType(type))
        );

        return slot.Push(owner, implementation, _logger);
    }

    public ImmutableArray<ServiceReplacement> Replacements =>
        [
            .. _replaced
                .OrderBy(x => x.Key.Name, StringComparer.Ordinal)
                .SelectMany(x => x.Value.Entries),
        ];
}

/// <summary>
/// One contract's slot: the host's implementation under a stack of plugins' replacements, the
/// last pushed in use. Copy-on-write, so a call reads one whole stack.
/// </summary>
internal sealed class ServiceSlot<T> : IServiceSlot<T>, IServiceSlotEntries
    where T : class
{
    private readonly Lock _gate = new();
    private volatile Entry[] _stack = [];

    public ServiceSlot(T @default)
    {
        Default = @default;
        Forwarder = SlotForwarder<T>.Create(this);
    }

    /// <summary>What the container gives for the contract: each call goes to <see cref="Current"/>.</summary>
    public T Forwarder { get; }

    public T Default { get; }

    public T Current => _stack is [.., var top] ? top.Implementation : Default;

    public string? Owner => _stack is [.., var top] ? top.Owner : null;

    public T Under(T replacement)
    {
        var stack = _stack;

        for (var i = stack.Length - 1; i >= 0; i--)
            if (ReferenceEquals(stack[i].Implementation, replacement))
                return i > 0 ? stack[i - 1].Implementation : Default;

        return Current;
    }

    public IEnumerable<ServiceReplacement> Entries
    {
        get
        {
            var stack = _stack;

            return stack.Select(
                (x, i) =>
                    new ServiceReplacement(
                        typeof(T).Name,
                        x.Owner,
                        x.Implementation.GetType().Name,
                        i == stack.Length - 1
                    )
            );
        }
    }

    public IDisposable Push(string owner, object implementation, ILogger logger)
    {
        // A plugin handing back what the host gave it replaces nothing (and would call itself).
        if (ReferenceEquals(implementation, Forwarder))
            return new Removal(this, null, logger);

        var entry = new Entry(owner, (T)implementation);
        Entry? previous;

        lock (_gate)
        {
            previous = _stack is [.., var top] ? top : null;
            _stack = [.. _stack, entry];
        }

        if (
            previous is not null
            && !string.Equals(previous.Owner, owner, StringComparison.OrdinalIgnoreCase)
        )
            logger.LogWarning(
                "Conflict: {Owner} replaces {Contract}, which {Previous} replaced too; {Owner}'s {Implementation} is used until it unloads, then {Previous}'s again",
                owner,
                typeof(T).Name,
                previous.Owner,
                owner,
                implementation.GetType().Name,
                previous.Owner
            );
        else
            logger.LogInformation(
                "{Owner} replaces the host's {Contract} with {Implementation}",
                owner,
                typeof(T).Name,
                implementation.GetType().Name
            );

        return new Removal(this, entry, logger);
    }

    private void Remove(Entry entry, ILogger logger)
    {
        string? owner;

        lock (_gate)
        {
            if (!_stack.Contains(entry))
                return;

            _stack = [.. _stack.Where(x => x != entry)];
            owner = Owner;
        }

        logger.LogInformation(
            "{Owner} no longer replaces {Contract}; {Now} is used",
            entry.Owner,
            typeof(T).Name,
            owner is null ? "the host's own" : $"{owner}'s"
        );
    }

    // A class: one plugin's two pushes of one implementation are two entries.
    private sealed class Entry(string owner, T implementation)
    {
        public string Owner { get; } = owner;

        public T Implementation { get; } = implementation;
    }

    private sealed class Removal(ServiceSlot<T> slot, Entry? entry, ILogger logger) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (entry is not null && Interlocked.Exchange(ref _disposed, 1) == 0)
                slot.Remove(entry, logger);
        }
    }
}

/// <summary>
/// The contract's forwarder: every call goes to the slot's implementation in use as it is made,
/// so a caller never holds on to a replaced one (or keeps an unloaded plugin's assembly alive).
/// </summary>
internal class SlotForwarder<T> : DispatchProxy
    where T : class
{
    private ServiceSlot<T> _slot = null!;

    internal static T Create(ServiceSlot<T> slot)
    {
        var forwarder = DispatchProxy.Create<T, SlotForwarder<T>>();

        ((SlotForwarder<T>)(object)forwarder)._slot = slot;

        return forwarder;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        try
        {
            return targetMethod!.Invoke(_slot.Current, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();

            throw;
        }
    }
}
