using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A chat style to add or change. Anything left null keeps what it was (or is off, for a new
/// one). <see cref="Files"/> sets bitmaps by file, as PNG, or removes one with null; files not
/// named are kept, or - for a new style - copied from <see cref="CopyFrom"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ChatStyleEdit
{
    [Id(0)]
    public int? ClientStyleId { get; init; }

    [Id(1)]
    public string? AssetId { get; init; }

    [Id(2)]
    public bool? ClubOnly { get; init; }

    [Id(3)]
    public bool? AmbassadorOnly { get; init; }

    [Id(4)]
    public bool? StaffOnly { get; init; }

    [Id(5)]
    public bool? Purchasable { get; init; }

    [Id(6)]
    public bool? System { get; init; }

    [Id(7)]
    public bool? StaffOverrideable { get; init; }

    [Id(8)]
    public bool? AllowHtml { get; init; }

    [Id(9)]
    public bool? Notification { get; init; }

    /// <summary>Its reg points as the client's JSON object.</summary>
    [Id(10)]
    public string? RegPoints { get; init; }

    [Id(11)]
    public IReadOnlyDictionary<string, byte[]?>? Files { get; init; }

    /// <summary>The style (row id) whose bitmaps a new one starts with.</summary>
    [Id(12)]
    public int? CopyFrom { get; init; }
}
