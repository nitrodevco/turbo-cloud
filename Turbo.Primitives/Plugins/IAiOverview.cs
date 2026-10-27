using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// The hotel's language models at a glance, for the admin panel (<c>docs/ai-sdk.md</c>, Usage): the
/// providers and whether each can be asked, the failover order, the plugins that declare AI and
/// whether the owner allowed them, and the usage log summed by plugin, provider and model. Never a key.
/// </summary>
public interface IAiOverview
{
    Task<AiOverview> GetAsync(int days, CancellationToken ct);
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

/// <summary>Usage summed by plugin, provider and model.</summary>
public sealed record AiUsageView(
    string Plugin,
    string Provider,
    string Model,
    int Calls,
    int Failed,
    long InputTokens,
    long OutputTokens,
    decimal Cost
);
