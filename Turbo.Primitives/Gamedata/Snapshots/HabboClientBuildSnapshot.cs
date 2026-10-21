using System;
using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// The client's libraries the hotel builds from Habbo's HTML client: each bundle, and the last
/// build - the release it read, when, and what it could not pack or build.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboClientBuildSnapshot
{
    [Id(0)]
    public required ImmutableArray<HabboClientBuiltLibrary> Libraries { get; init; }

    /// <summary>The release the last build read; null before the first.</summary>
    [Id(1)]
    public string? Version { get; init; }

    [Id(2)]
    public DateTime? BuiltAt { get; init; }

    /// <summary>The window templates' problems: a bitmap no library has, a layout the client would refuse.</summary>
    [Id(3)]
    public required ImmutableArray<string> Problems { get; init; }

    /// <summary>What the last build left unbuilt, and why.</summary>
    [Id(4)]
    public required ImmutableArray<string> Notes { get; init; }

    /// <summary>The AIR client the templates took from; null when they took nothing from one.</summary>
    [Id(5)]
    public string? AirVersion { get; init; }

    /// <summary>The bitmaps taken from it that the HTML client's libraries lack.</summary>
    [Id(6)]
    public int AirImages { get; init; }

    /// <summary>The layouts taken from it in place of the HTML client's.</summary>
    [Id(7)]
    public int AirLayouts { get; init; }
}

/// <summary>One bundle built from the client: <c>avatar</c>, <c>variable-fx</c>, <c>texts</c>, <c>chat</c>, <c>template</c> or <c>window-manager</c> (its templates and the theme).</summary>
[GenerateSerializer, Immutable]
public sealed record HabboClientBuiltLibrary
{
    [Id(0)]
    public required string Library { get; init; }

    [Id(1)]
    public required string Kind { get; init; }

    /// <summary>The hotel's own upload stands in its place: a build leaves it alone.</summary>
    [Id(2)]
    public required bool Uploaded { get; init; }

    [Id(3)]
    public required long Size { get; init; }

    [Id(4)]
    public DateTime? UpdatedAt { get; init; }
}
