namespace Turbo.Primitives.Inventory;

/// <summary>What came of giving a player an avatar effect.</summary>
public enum EffectGrantResult
{
    /// <summary>The copies were added, or the effect made permanent.</summary>
    Granted = 0,

    /// <summary>The effect id is outside the hotel's range, or the grant has no copies and is not permanent.</summary>
    Invalid = 1,

    /// <summary>The player already has the effect for good; a copy would never be used.</summary>
    AlreadyPermanent = 2,

    /// <summary>The grant would pass the copy cap of that effect, or the cap of different effects.</summary>
    LimitReached = 3,
}
