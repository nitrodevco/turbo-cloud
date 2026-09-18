namespace Turbo.Primitives.Furniture;

/// <summary>
/// The map keys the client's present logic reads, and the extra-data section where the server
/// keeps the wrapped item. A gift is a present item whose <see cref="STORAGE_SECTION"/> holds
/// <c>{"ItemId": n}</c> for a furniture row owned by the same player and in no room.
/// </summary>
public static class PresentData
{
    public const string MESSAGE = "MESSAGE";
    public const string PRODUCT_CODE = "PRODUCT_CODE";
    public const string EXTRA_PARAM = "EXTRA_PARAM";
    public const string PURCHASER_NAME = "PURCHASER_NAME";
    public const string PURCHASER_FIGURE = "PURCHASER_FIGURE";
    public const string TRUSTED_SENDER = "TRUSTED_SENDER";
    public const string STORAGE_SECTION = "gift";
}
