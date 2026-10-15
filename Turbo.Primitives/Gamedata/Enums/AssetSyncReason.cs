namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>Why a sync would download one of Habbo's libraries.</summary>
public enum AssetSyncReason
{
    /// <summary>The hotel has no file of it: never taken, deleted, or failed at an older revision or in a way worth trying again.</summary>
    New = 0,

    /// <summary>The hotel has it at another revision than Habbo serves.</summary>
    Updated = 1,
}
