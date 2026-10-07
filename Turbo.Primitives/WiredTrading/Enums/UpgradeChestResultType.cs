namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>Result of a wired chest upgrade; anything but <see cref="Success"/> shows <c>wiredchests.upgrade.result.error.{id}</c>.</summary>
public enum UpgradeChestResultType
{
    Success = 0,
    FeatureDisabled = 1,
    AtMaximumCapacity = 2,
    SafetyLocked = 3,

    /// <summary>The client has no text for this code beyond "Error 4".</summary>
    Error4 = 4,
    InsufficientCredits = 5,
    InsufficientDiamonds = 6,
    NotOwner = 7,

    /// <summary>The client has no text for this code beyond "Error 8".</summary>
    Error8 = 8,

    /// <summary>The client has no text for this code beyond "Error 9".</summary>
    Error9 = 9,

    /// <summary>A starter chest cannot be upgraded.</summary>
    StarterChest = 10,
}
