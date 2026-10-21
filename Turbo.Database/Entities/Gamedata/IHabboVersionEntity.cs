using System;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One version of a file of Habbo's as a check found it: its furniture data
/// (<see cref="HabboReleaseEntity"/>), texts, product data, figure data or external variables. A
/// check that finds the same file again only moves <see cref="CheckedAt"/>; the newest of a
/// domain is the one with the highest id.
/// </summary>
public interface IHabboVersionEntity
{
    /// <summary>The Habbo hotel it came from: <c>habbo.&lt;domain&gt;</c>.</summary>
    public string Domain { get; }

    public DateTime CheckedAt { get; set; }

    /// <summary>When it was last taken in; null before that.</summary>
    public DateTime? ImportedAt { get; set; }

    /// <summary>
    /// The hash Habbo's <c>/gamedata/hashes</c> listed for the file when this version was last
    /// found; null when it could not be read.
    /// </summary>
    public string? HabboHash { get; set; }
}
