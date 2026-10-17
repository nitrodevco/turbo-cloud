using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// A library Habbo serves, as a sync lists it: its kind and name, the revision it is at, and the
/// ids that load it as a row keeps them (null for a furniture), and - where it is not its name -
/// the name its file is downloaded under.
/// </summary>
internal sealed record HabboLibrary(
    AssetBundleKind Kind,
    string Name,
    string Revision,
    string? Ids,
    string? Download = null,
    string? MapEntries = null
);
