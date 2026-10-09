using System.Collections.Generic;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// Changes to the hotel view saved together: <c>landing.view.*</c> variables, each a value as JSON
/// or null to remove it, and the external texts its widgets show, each a value or null to remove
/// it. Keys not named stay as they are.
/// </summary>
public sealed record HotelViewEdit
{
    public IReadOnlyDictionary<string, string?> Variables { get; init; } =
        new Dictionary<string, string?>();

    public IReadOnlyDictionary<string, string?> Texts { get; init; } =
        new Dictionary<string, string?>();
}
