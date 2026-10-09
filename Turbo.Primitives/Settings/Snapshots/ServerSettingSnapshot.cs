using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Settings.Enums;

namespace Turbo.Primitives.Settings.Snapshots;

/// <summary>
/// One setting of the server, as the admin panel shows it. Values are JSON (<c>"text"</c>,
/// <c>true</c>, <c>120</c>, <c>["a", "b"]</c>); a secret's are always null.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ServerSettingSnapshot
{
    /// <summary>Its configuration path: <c>Turbo:Rooms:MaxUsersPerRoom</c>.</summary>
    [Id(0)]
    public required string Path { get; init; }

    /// <summary>The section it is in: <c>Turbo:Rooms</c>.</summary>
    [Id(1)]
    public required string Section { get; init; }

    /// <summary>
    /// What kind of value it takes: <c>string</c>, <c>bool</c>, <c>integer</c>, <c>number</c>,
    /// <c>enum</c>, <c>duration</c>, <c>list</c> or <c>map</c>.
    /// </summary>
    [Id(2)]
    public required string Kind { get; init; }

    /// <summary>The names an <c>enum</c> takes.</summary>
    [Id(3)]
    public required ImmutableArray<string> Options { get; init; }

    /// <summary>What the setting does, from its config class's documentation.</summary>
    [Id(4)]
    public required string Summary { get; init; }

    [Id(5)]
    public required bool Secret { get; init; }

    /// <summary>Shown but not changed in the panel (<see cref="StartupSettingAttribute"/>).</summary>
    [Id(6)]
    public required bool Startup { get; init; }

    /// <summary>The default its config class ships with.</summary>
    [Id(7)]
    public string? Default { get; init; }

    /// <summary>The value configured now, every source applied.</summary>
    [Id(8)]
    public string? Value { get; init; }

    /// <summary>The value the server is using now: what it read as it started.</summary>
    [Id(9)]
    public string? Running { get; init; }

    /// <summary>Whether it has a value at all: for a secret, all that is shown.</summary>
    [Id(10)]
    public required bool IsSet { get; init; }

    [Id(11)]
    public required ServerSettingSource Source { get; init; }

    /// <summary>
    /// Which file or variable sets it: <c>appsettings.json</c>, <c>TURBO__Turbo__Rooms__MaxUsers</c>,
    /// <c>command line</c>. Null for the default and the panel.
    /// </summary>
    [Id(12)]
    public string? SourceName { get; init; }

    /// <summary>The value configured differs from the one running: it applies after a restart.</summary>
    [Id(13)]
    public required bool PendingRestart { get; init; }
}
