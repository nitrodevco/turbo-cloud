namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>
/// Which items an open furni chest shows floating above it
/// (<c>wiredchests.settings.appearance.preview.*</c>). The "distinct" modes prefer showing
/// different item types.
/// </summary>
public enum WiredChestPreviewMode
{
    None = 0,
    Random = 1,
    RandomDistinct = 2,
    MostRecent = 3,
    MostRecentDistinct = 4,
    Oldest = 5,
    OldestDistinct = 6,

    /// <summary>The random items wired would give next.</summary>
    NextRandom = 7,
}
