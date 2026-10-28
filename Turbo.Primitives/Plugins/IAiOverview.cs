using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// The hotel's language models for the admin panel (<c>docs/ai-sdk.md</c>, The panel): the
/// providers and whether each can be asked, the failover order, the plugins that declare AI and
/// whether the owner allowed them, and the usage log summed by day, plugin, provider and model;
/// a provider's key set without the others' (a key is never read back), and a provider tried.
/// </summary>
public interface IAiOverview
{
    Task<AiOverview> GetAsync(int days, CancellationToken ct);

    /// <summary>
    /// Sets <paramref name="provider"/>'s key (<c>Turbo:Ai:Keys</c>), or with a blank one removes
    /// it, keeping every other provider's. Throws <see cref="ArgumentException"/> for a provider
    /// that is not configured, or keys the environment sets.
    /// </summary>
    Task SetKeyAsync(string provider, string? key, PlayerId by, CancellationToken ct);

    /// <summary>
    /// Asks <paramref name="provider"/> for one token with its key and model, to see that it
    /// answers. The call is kept in the usage log as the panel's (<see cref="TEST_PLUGIN"/>).
    /// </summary>
    Task<AiConnectionTest> TestAsync(string provider, CancellationToken ct);

    /// <summary>What the usage log names the panel's connection tests by, in place of a plugin.</summary>
    public const string TEST_PLUGIN = "(panel)";
}

public sealed record AiOverview(
    ImmutableArray<AiProviderView> Providers,
    ImmutableArray<string> Failover,
    ImmutableArray<AiPluginView> Plugins,
    ImmutableArray<AiUsageView> Usage,
    DateTime Since
);

/// <summary>A provider: its driver, address and model, whether a key is set (never the key) and whether it can be asked.</summary>
public sealed record AiProviderView(
    string Name,
    string Driver,
    string Url,
    string Model,
    bool HasKey,
    bool Usable
);

/// <summary>A plugin declaring AI (its manifest's purpose), and whether the hotel allowed it.</summary>
public sealed record AiPluginView(string Key, string Name, string Purpose, bool Allowed);

/// <summary>Usage summed by UTC day, plugin, provider and model.</summary>
public sealed record AiUsageView(
    DateTime Day,
    string Plugin,
    string Provider,
    string Model,
    int Calls,
    int Failed,
    long InputTokens,
    long OutputTokens,
    decimal Cost
);
