namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>When a chest is drawn open (<c>wiredchests.settings.appearance.state.*</c>).</summary>
public enum WiredChestStateMode
{
    /// <summary>Open while someone has its window open.</summary>
    OpenWhenViewed = 0,
    AlwaysOpen = 1,
    AlwaysClosed = 2,

    /// <summary>Wired sets the state like any other furni's.</summary>
    ControlledByWired = 3,
}
