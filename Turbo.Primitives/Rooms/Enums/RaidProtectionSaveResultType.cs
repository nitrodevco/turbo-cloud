namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// The answer to a raid protection save. The client closes its window on <see cref="Saved"/> and
/// otherwise shows <c>raid.protection.settings.save.fail.&lt;code&gt;</c>.
/// </summary>
public enum RaidProtectionSaveResultType
{
    Saved = 0,

    /// <summary>"Raid protection is currently disabled."</summary>
    FeatureDisabled = 1,

    /// <summary>"This room is no longer available."</summary>
    RoomUnavailable = 2,

    /// <summary>"You do not have permission to manage Raid protection in this room."</summary>
    NotAllowed = 3,

    /// <summary>"Choose valid Raid protection settings before saving."</summary>
    Invalid = 4,

    /// <summary>"Confirm that you want to enable anti-raid protection, then save again."</summary>
    NotConfirmed = 5,

    /// <summary>"Raid protection settings could not be saved. The previous settings remain active."</summary>
    Failed = 6,
}
