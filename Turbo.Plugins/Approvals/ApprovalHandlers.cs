using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Approvals;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins.Approvals;

/// <summary>
/// Who carries out each kind of approved request: core's own (<see cref="IApprovalHandler"/>
/// services) and the live plugins' (found in their assemblies as they load, gone as they start to
/// stop). One kind is one owner's.
/// </summary>
internal sealed partial class ApprovalHandlers(IEnumerable<IApprovalHandler> core)
{
    public const string CORE = "core";

    private readonly ConcurrentDictionary<string, ImmutableArray<IApprovalHandler>> _live = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly Lazy<IReadOnlyDictionary<string, IApprovalHandler>> _core = new(() =>
    {
        var byKind = new Dictionary<string, IApprovalHandler>(StringComparer.Ordinal);

        foreach (var handler in core)
        {
            CheckKind(handler.Kind, CORE);

            if (!byKind.TryAdd(handler.Kind, handler))
                throw new InvalidOperationException(
                    $"Core has two approval handlers for the kind {handler.Kind}."
                );
        }

        return byKind;
    });

    /// <summary>The live handler of <paramref name="kind"/> and its owner (a plugin's key, or <see cref="CORE"/>); null when none is live.</summary>
    public (IApprovalHandler Handler, string Owner)? Find(string kind)
    {
        if (_core.Value.TryGetValue(kind, out var own))
            return (own, CORE);

        foreach (var (key, handlers) in _live)
            if (handlers.FirstOrDefault(x => x.Kind == kind) is { } handler)
                return (handler, key);

        return null;
    }

    /// <summary>
    /// The handlers <paramref name="asm"/> declares, each built with <paramref name="services"/>.
    /// Throws <see cref="InvalidOperationException"/> for a kind that can't be a request's, one the
    /// plugin has twice, or one core or another live plugin carries out.
    /// </summary>
    public ImmutableArray<IApprovalHandler> Build(
        string key,
        Assembly asm,
        IServiceProvider services
    )
    {
        var handlers = new List<IApprovalHandler>();

        foreach (var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IApprovalHandler)))
        {
            var handler = (IApprovalHandler)ActivatorUtilities.CreateInstance(services, concrete);

            CheckKind(handler.Kind, $"The plugin {key}'s {concrete.Name}");

            if (handlers.Any(x => x.Kind == handler.Kind))
                throw new InvalidOperationException(
                    $"The plugin {key} has two approval handlers for the kind {handler.Kind}."
                );

            if (
                Find(handler.Kind) is { Owner: var owner }
                && !string.Equals(owner, key, StringComparison.OrdinalIgnoreCase)
            )
                throw new InvalidOperationException(
                    $"The plugin {key}'s approval handler for {handler.Kind} has the kind {owner} carries out: a request names one handler by its kind. Name it {key}.{handler.Kind}."
                );

            handlers.Add(handler);
        }

        return [.. handlers];
    }

    /// <summary>Makes the plugin's handlers live; disposing the result takes them away.</summary>
    public IDisposable Live(string key, ImmutableArray<IApprovalHandler> handlers)
    {
        _live[key] = handlers;

        return new Removal(this, key, handlers);
    }

    /// <summary>The plugin is reloading, stopping, turned off, failed or gone: it carries nothing out.</summary>
    public void Off(string key) => _live.TryRemove(key, out _);

    /// <summary>Whether <paramref name="kind"/> may name a request's handler.</summary>
    public static bool IsKind(string? kind) =>
        kind is { Length: > 0 and <= ApprovalRequest.KIND_MAX_LENGTH }
        && KindPattern().IsMatch(kind);

    private static void CheckKind(string? kind, string who)
    {
        if (!IsKind(kind))
            throw new InvalidOperationException(
                $"{who} carries out the approval kind \"{kind}\": a kind is 1 to {ApprovalRequest.KIND_MAX_LENGTH} lower-case letters, digits, '.', '_', '-' or ':', starting with a letter or digit."
            );
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]*$")]
    private static partial Regex KindPattern();

    private sealed class Removal(
        ApprovalHandlers owner,
        string key,
        ImmutableArray<IApprovalHandler> handlers
    ) : IDisposable
    {
        public void Dispose()
        {
            // Only the version it made live: a newer one may already have replaced it.
            if (owner._live.TryGetValue(key, out var live) && live == handlers)
                owner._live.TryRemove(key, out _);
        }
    }
}
