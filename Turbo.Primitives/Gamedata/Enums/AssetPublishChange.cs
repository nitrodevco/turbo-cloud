namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>Why a publish sends a bundle to a target.</summary>
public enum AssetPublishChange
{
    /// <summary>It was never sent to the target.</summary>
    New = 0,

    /// <summary>The hotel's copy changed since it was sent.</summary>
    Changed = 1,

    /// <summary>It was sent, but the target no longer holds it (found only by a publish, which looks).</summary>
    Gone = 2,
}
