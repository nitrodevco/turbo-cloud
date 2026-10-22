using System.Collections.Generic;

namespace Turbo.Players.Configuration;

/// <summary>
/// One step of the new user gift offer (<c>Turbo:Onboarding:Gifts</c>): from its
/// <see cref="Day"/> on, a new player is offered its gifts and picks one, once. Habbo's client
/// shows a step's gifts side by side under "Get your free gift!" and, with more than one step due,
/// one step after another ("Choose gift option 1/2").
/// </summary>
public sealed class NuxGiftStep
{
    /// <summary>The day after sign-up the step is offered from: 1 is the day the player signed up.</summary>
    public int Day { get; set; } = 1;

    /// <summary>Tells the steps of one day apart; each day and step is offered and taken once.</summary>
    public int Step { get; set; } = 1;

    /// <summary>The gifts to pick from; Habbo's window is laid out for three.</summary>
    public List<NuxGift> Options { get; set; } = [];
}

/// <summary>One gift of a step: its picture and what picking it gives.</summary>
public sealed class NuxGift
{
    /// <summary>
    /// The gift's picture, a path under the client's <c>image.library.url</c>
    /// (<c>nux/gift_cat.png</c>); empty for none.
    /// </summary>
    public string Thumbnail { get; set; } = string.Empty;

    /// <summary>What the gift is made of; the client names it by its products, joined by " + ".</summary>
    public List<NuxGiftProduct> Products { get; set; } = [];
}

/// <summary>A product of a gift: how the client names it and what the player is given for it.</summary>
public sealed class NuxGiftProduct
{
    /// <summary>
    /// The product the client names the gift by (its product data, else the text
    /// <c>product_&lt;code&gt;_name</c>), and the furni definition given when <see cref="Furni"/>
    /// and <see cref="Pet"/> are empty.
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>A text the client names the product by instead (Habbo's <c>nux.gift.cat</c>); empty for none.</summary>
    public string LocalizationKey { get; set; } = string.Empty;

    /// <summary>The furni definition given, by name; empty gives <see cref="ProductCode"/>'s.</summary>
    public string Furni { get; set; } = string.Empty;

    /// <summary>
    /// A pet given instead of a furni, as Habbo writes a pet figure: "type palette colour"
    /// ("1 0 ffffff").
    /// </summary>
    public string Pet { get; set; } = string.Empty;

    /// <summary>The name the pet is given; empty for <see cref="DefaultPetName"/>.</summary>
    public string PetName { get; set; } = string.Empty;

    /// <summary>How many of the furni (or pets) are given.</summary>
    public int Amount { get; set; } = 1;

    public const string DefaultPetName = "Pet";
}
