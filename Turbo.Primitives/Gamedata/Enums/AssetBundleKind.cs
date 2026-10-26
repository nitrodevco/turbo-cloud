namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>
/// What an asset file is, which is also the folder it is kept and served in, laid out as Nitro
/// Studio publishes a hotel's assets. The first are <c>.nitro</c> bundles, named without their
/// extension; the rest (<see cref="AssetBundleKindExtensions.IsBundle"/> false) are files as Habbo
/// serves them, named with theirs.
/// </summary>
public enum AssetBundleKind
{
    /// <summary>A furniture's library, named as its classname without the <c>*N</c> colour (<c>bundled/furniture</c>).</summary>
    Furniture = 0,

    /// <summary>An avatar effect's library, named as the effect map's <c>lib</c> (<c>bundled/effects</c>).</summary>
    Effect = 1,

    /// <summary>A pet type's library, named as <c>pet.configuration</c> lists it (<c>bundled/pet</c>).</summary>
    Pet = 2,

    /// <summary>
    /// A clothing library (<c>hh_human_body</c>, <c>shirt_U_...</c>), named as the figure map's
    /// <c>lib</c>; the parts figuredata's sets use are drawn from it (<c>bundled/figures</c>).
    /// </summary>
    Figure = 3,

    /// <summary>
    /// A library of the client's own UI (<c>bundled/templates</c>), named as the client asks for it:
    /// the chat bubbles' <c>habbo-free-flow-chat-com</c>, built from the hotel's chat styles.
    /// </summary>
    Template = 4,

    /// <summary>
    /// A library of the room's own (<c>bundled/room</c>): the room content
    /// (<c>HabboRoomContent</c>) and the objects the room engine draws itself (<c>TileCursor</c>, ...).
    /// </summary>
    Room = 5,

    /// <summary>
    /// A furniture's icon (<c>images/furni-icons</c>): <c>&lt;asset&gt;_icon.png</c>, and
    /// <c>&lt;asset&gt;_&lt;N&gt;_icon.png</c> for its colour <c>*N</c>, drawn from its bundle.
    /// </summary>
    FurniIcon = 6,

    /// <summary>An icon of the catalog's tree (<c>images/catalog-icons</c>): <c>icon_&lt;n&gt;.png</c>.</summary>
    CatalogIcon = 7,

    /// <summary>A badge's picture (<c>images/badges</c>): <c>&lt;code&gt;.gif</c>.</summary>
    Badge = 8,

    /// <summary>A catalog page's header or teaser (<c>images/catalog</c>): <c>&lt;name&gt;.gif</c>.</summary>
    CatalogImage = 9,

    /// <summary>A trax machine sample (<c>sounds</c>): <c>sound_machine_sample_&lt;n&gt;.mp3</c>.</summary>
    Sound = 10,

    /// <summary>
    /// A picture of the client's image library (<c>c_images</c>, what <c>image.library.url</c>
    /// names): the hotel view's, the notices' and the catalog front page's promo pictures, named
    /// with their folders under it (<c>web_promo/summer.png</c>), as Habbo serves them.
    /// </summary>
    LibraryImage = 11,
}
