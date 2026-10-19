using Orleans;

namespace Turbo.Primitives.Inventory.Snapshots;

/// <summary>
/// A sheet taken off a post-it pad (<c>IInventoryGrain.TakePostItSheetAsync</c>): the pad as it
/// was before, and how many sheets it has left. None left means the last sheet was the pad itself,
/// which was used up. Handed back to give the sheet back when nothing came of it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PostItSheetTakenSnapshot
{
    [Id(0)]
    public required FurnitureItemSnapshot Pad { get; init; }

    [Id(1)]
    public required int SheetsLeft { get; init; }

    /// <summary>Whether the pad itself went with its last sheet.</summary>
    public bool UsedUpPad => SheetsLeft == 0;
}
