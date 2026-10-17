using System;
using System.Collections.Immutable;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>How one plugin found on disk stands, as of the last scan or action on it.</summary>
internal sealed record PluginStatus(
    PluginManifest Manifest,
    string Folder,
    PluginStateType State,
    string? Error,
    ImmutableArray<string> Migrations,
    DateTime? LoadedAtUtc
);
