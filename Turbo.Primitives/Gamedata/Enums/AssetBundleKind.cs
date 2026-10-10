namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>What a <c>.nitro</c> bundle holds, which is also the folder it is kept and served in.</summary>
public enum AssetBundleKind
{
    /// <summary>A furniture's library, named as its classname without the <c>*N</c> colour (<c>bundled/furniture</c>).</summary>
    Furniture = 0,

    /// <summary>An avatar effect's library, named as the effect map's <c>lib</c> (<c>bundled/effects</c>).</summary>
    Effect = 1,

    /// <summary>A pet type's library, named as <c>pet.configuration</c> lists it (<c>bundled/pet</c>).</summary>
    Pet = 2,
}
