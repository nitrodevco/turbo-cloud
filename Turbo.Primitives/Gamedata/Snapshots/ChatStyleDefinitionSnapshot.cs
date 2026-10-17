using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A chat bubble style whole: who may speak with it (the server's flags), how the client draws it
/// (its reg points, flags and bitmaps, as chat-style-definitions.json has them), and how many own
/// it and have it as their bubble.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ChatStyleDefinitionSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int ClientStyleId { get; init; }

    /// <summary>The client's asset id: its bitmaps are <c>style_&lt;assetId&gt;_&lt;file&gt;</c>.</summary>
    [Id(2)]
    public required string AssetId { get; init; }

    [Id(3)]
    public required bool ClubOnly { get; init; }

    [Id(4)]
    public required bool AmbassadorOnly { get; init; }

    /// <summary>The server's own: only those who may use the staff bubble. The client has no such flag.</summary>
    [Id(5)]
    public required bool StaffOnly { get; init; }

    [Id(6)]
    public required bool Purchasable { get; init; }

    [Id(7)]
    public required bool System { get; init; }

    [Id(8)]
    public required bool StaffOverrideable { get; init; }

    [Id(9)]
    public required bool AllowHtml { get; init; }

    [Id(10)]
    public required bool Notification { get; init; }

    /// <summary>Its reg points as the client's JSON object; null before it has a look.</summary>
    [Id(11)]
    public string? RegPoints { get; init; }

    /// <summary>The bitmaps it has, by file (<c>chat_bubble_base</c>, ...).</summary>
    [Id(12)]
    public required ImmutableArray<string> Bitmaps { get; init; }

    [Id(13)]
    public required int SortOrder { get; init; }

    [Id(14)]
    public required int Owners { get; init; }

    [Id(15)]
    public required int Preferring { get; init; }

    /// <summary>Whether the bundle carries it: it needs reg points and its three bitmaps.</summary>
    [Id(16)]
    public required bool Drawable { get; init; }

    /// <summary>Its look came from Habbo's HTML client; false for one of the hotel's own, or one not yet taken from it.</summary>
    [Id(17)]
    public bool FromClient { get; init; }

    /// <summary>Taken from the client, then changed here: a new release leaves it as it is unless told to replace it.</summary>
    [Id(18)]
    public bool ChangedHere { get; init; }
}
