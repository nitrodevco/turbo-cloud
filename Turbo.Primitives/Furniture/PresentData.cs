namespace Turbo.Primitives.Furniture;

/// <summary>
/// The map keys the client's present logic reads. The wrapped item itself is kept in the
/// <c>PresentStorage</c> extra data section.
/// </summary>
public static class PresentData
{
    public const string MESSAGE = "MESSAGE";
    public const string PRODUCT_CODE = "PRODUCT_CODE";
    public const string EXTRA_PARAM = "EXTRA_PARAM";
    public const string PURCHASER_NAME = "PURCHASER_NAME";
    public const string PURCHASER_FIGURE = "PURCHASER_FIGURE";
    public const string TRUSTED_SENDER = "TRUSTED_SENDER";
}
