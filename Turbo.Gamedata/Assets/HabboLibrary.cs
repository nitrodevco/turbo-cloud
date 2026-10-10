using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// A library Habbo serves, as a sync lists it: its kind and name, the revision it is at, and the
/// ids that load it as a row keeps them (null for a furniture).
/// </summary>
internal sealed record HabboLibrary(
    AssetBundleKind Kind,
    string Name,
    string Revision,
    string? Ids
);
