namespace Turbo.Catalog.Configuration;

/// <summary>
/// The gift dialog's choices. Furniture is named by definition name, so a hotel's ids do not
/// matter; a name with no definition is left out of what the client is offered.
/// </summary>
public sealed class GiftWrappingConfig
{
    /// <summary>Whether offers can be bought as gifts at all.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Credits a paid wrapping costs on top of the offer; the free box costs nothing.</summary>
    public int Price { get; init; } = 1;

    /// <summary>The coloured presents a paid wrapping is made of (the client's colour grid).</summary>
    public string[] WrapperNames { get; init; } =
    [
        "present_wrap*1",
        "present_wrap*2",
        "present_wrap*3",
        "present_wrap*4",
        "present_wrap*5",
        "present_wrap*6",
        "present_wrap*7",
        "present_wrap*8",
        "present_wrap*9",
        "present_wrap*10",
    ];

    /// <summary>Box styles of a paid wrapping, as the client's <c>gift_wrapping_new.box.*</c> texts number them.</summary>
    public int[] BoxTypes { get; init; } = [0, 1, 2, 3, 4, 5, 6, 8];

    /// <summary>Ribbons of a paid wrapping, as the client's <c>gift_wrapping_new.ribbon.*</c> texts number them.</summary>
    public int[] RibbonTypes { get; init; } = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    /// <summary>The free boxes; the client picks one of them at random.</summary>
    public string[] DefaultNames { get; init; } =
    [
        "present_gen",
        "present_gen1",
        "present_gen2",
        "present_gen3",
        "present_gen4",
        "present_gen5",
        "present_gen6",
    ];

    /// <summary>The longest note on a gift tag, in characters; a longer one is refused.</summary>
    public int MessageMaxLength { get; init; } = 140;
}
