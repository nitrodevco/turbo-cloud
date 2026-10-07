namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>How much a give-from-chest box hands out.</summary>
public enum WiredChestRewardingMode
{
    SpecifiedAmount = 0,

    /// <summary>Everything the chests hold; refused when more than one user is to receive it.</summary>
    All = 1,
}
