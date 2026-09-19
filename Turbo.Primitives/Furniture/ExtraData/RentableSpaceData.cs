namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// Who rents a rentable space and until when, under <see cref="SECTION"/> in the item extra
/// data. No renter, or a time that has passed, is a free space.
/// </summary>
public sealed record RentableSpaceData
{
    public const string SECTION = "rentable_space";

    public int RenterId { get; init; }
    public string RenterName { get; init; } = string.Empty;

    /// <summary>Unix seconds.</summary>
    public long ExpiresAt { get; init; }
}
