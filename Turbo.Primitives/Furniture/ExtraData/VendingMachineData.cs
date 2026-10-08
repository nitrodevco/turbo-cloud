using System.Collections.Immutable;

namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a vending furni hands out, under <see cref="SECTION"/> in its definition's extra data. It
/// is Sulake's furni data for the furni: the hand items its <c>&lt;drinks&gt;</c> lists (one is
/// drawn at random each time: a fridge's drink, ice cream or carrot) and whether its server class
/// plays a dispensing state (<c>VendingMachineFurni</c> and <c>IceCreamMachineFurni</c>, whose
/// assets animate state 1) or hands the item over as it stands (<c>HandItemProviderFurni</c>, a
/// static furni such as a chilli bowl).
/// </summary>
public sealed record VendingMachineData
{
    public const string SECTION = "vending";

    /// <summary>The hand items it gives, by id; one is drawn at random.</summary>
    public ImmutableArray<int> HandItems { get; init; } = [];

    /// <summary>Whether it shows state 1 while it hands an item out.</summary>
    public bool Animates { get; init; } = true;
}
