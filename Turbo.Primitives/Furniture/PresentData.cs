using System;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// The map keys the client's present logic reads. The box and ribbon are kept in the
/// <c>PresentStorage</c> extra data section, and the wrapped item is the row the present holds.
/// </summary>
public static class PresentData
{
    /// <summary>
    /// The logic of a present. Here rather than in the room module because an inventory lists a
    /// present with its box and ribbon before it is ever placed.
    /// </summary>
    public const string LOGIC_NAME = "present";

    public const string MESSAGE = "MESSAGE";
    public const string PRODUCT_CODE = "PRODUCT_CODE";
    public const string EXTRA_PARAM = "EXTRA_PARAM";
    public const string PURCHASER_NAME = "PURCHASER_NAME";
    public const string PURCHASER_FIGURE = "PURCHASER_FIGURE";
    public const string TRUSTED_SENDER = "TRUSTED_SENDER";

    /// <summary>The <see cref="TRUSTED_SENDER"/> value the client reads as trusted; anything else is not.</summary>
    public const string TRUSTED = "true";

    public static bool IsPresent(string? logicName) =>
        string.Equals(logicName, LOGIC_NAME, StringComparison.Ordinal);
}
