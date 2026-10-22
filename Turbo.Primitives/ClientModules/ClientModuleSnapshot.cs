using Orleans;

namespace Turbo.Primitives.ClientModules;

/// <summary>One client module as a client is told of it (<c>TurboClientModulesMessage</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record ClientModuleSnapshot
{
    /// <summary>The plugin's key.</summary>
    [Id(0)]
    public required string Key { get; init; }

    /// <summary>The entry file's address: absolute, or site-relative (<c>/gamedata/plugins/...</c>).</summary>
    [Id(1)]
    public required string Url { get; init; }

    /// <summary>The plugin's version, as its manifest says.</summary>
    [Id(2)]
    public required string Version { get; init; }

    /// <summary>The entry file's SHA-256, lowercase hex; empty when it is not to be checked.</summary>
    [Id(3)]
    public required string Sha256 { get; init; }
}
