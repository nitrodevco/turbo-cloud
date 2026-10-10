namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>Which bundles the panel's list shows.</summary>
public enum AssetBundleStatusFilter
{
    All = 0,

    /// <summary>Those with a file and no error.</summary>
    Ok = 1,

    /// <summary>Those that could not be downloaded or converted.</summary>
    Failed = 2,

    /// <summary>Those the hotel names nowhere: a furniture no definition names, a pet type with no breeds.</summary>
    Unused = 3,
}
