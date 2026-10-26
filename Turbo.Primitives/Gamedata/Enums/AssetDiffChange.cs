namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>How a part of a file (a window, a text, an image) differs from the target's copy to the hotel's.</summary>
public enum AssetDiffChange
{
    /// <summary>The hotel's copy has it and the target's does not.</summary>
    Added = 0,

    /// <summary>The target's copy has it and the hotel's does not.</summary>
    Removed = 1,

    /// <summary>Both have it, holding something else.</summary>
    Changed = 2,
}
