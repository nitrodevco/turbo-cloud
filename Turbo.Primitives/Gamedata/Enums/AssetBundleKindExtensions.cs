namespace Turbo.Primitives.Gamedata.Enums;

public static class AssetBundleKindExtensions
{
    /// <summary>Whether a file of the kind is a <c>.nitro</c> bundle, named without its extension.</summary>
    public static bool IsBundle(this AssetBundleKind kind) =>
        kind
            is AssetBundleKind.Furniture
                or AssetBundleKind.Effect
                or AssetBundleKind.Pet
                or AssetBundleKind.Figure
                or AssetBundleKind.Template
                or AssetBundleKind.Room;
}
