namespace Turbo.Primitives.Movements;

/// <summary>
/// The cause codes core writes. A marker row moves nothing (asset id 0, amount 0, both places
/// none): it tells a consumer that recording started or stopped, so "nothing happened" and
/// "nothing was recorded" read differently. Its cause ref says why. The other codes are stable:
/// a consumer keys on them, so never rename one.
/// </summary>
public static class MovementCauses
{
    public const string RECORDING_STARTED = "recording.started";
    public const string RECORDING_STOPPED = "recording.stopped";

    /// <summary>A marker's cause ref: the server started or stopped while recording was on.</summary>
    public const string MARKER_REF_SERVER = "server";

    /// <summary>A marker's cause ref: <c>Turbo:Movements:Enabled</c> was changed while the server ran.</summary>
    public const string MARKER_REF_SETTING = "setting";

    public const string CATALOG_PURCHASE = "catalog.purchase";
    public const string CATALOG_GIFT = "catalog.gift";
    public const string CATALOG_ROOM_AD = "catalog.room_ad";
    public const string CATALOG_TARGETED_OFFER = "catalog.targeted_offer";
    public const string CATALOG_LTD_RAFFLE = "catalog.ltd_raffle";
    public const string CATALOG_VOUCHER = "catalog.voucher";
    public const string CATALOG_HABBICON = "catalog.habbicon";
    public const string CAMERA_PURCHASE = "camera.purchase";
    public const string CAMERA_PUBLISH = "camera.publish";
    public const string GROUP_CREATE = "group.create";
    public const string VAULT_CLAIM = "vault.claim";

    public const string MARKET_TOKENS = "market.tokens";
    public const string MARKET_BUY = "market.buy";
    public const string MARKET_REDEEM = "market.redeem";

    public const string ROOM_RENT = "room.rent";
    public const string USE_CREDIT_FURNI = "use.credit_furni";
    public const string USE_CRACKABLE = "use.crackable";

    public const string WIRED_CHEST_UPGRADE = "wired.chest_upgrade";
    public const string WIRED_CHEST_WITHDRAW = "wired.chest_withdraw";

    public const string REWARD_TRACK = "reward.track";
    public const string REWARD_QUEST = "reward.quest";
    public const string REWARD_DAILY_TASK = "reward.daily_task";
    public const string REWARD_ACHIEVEMENT = "reward.achievement";

    public const string STAFF_GIVE_CURRENCY = "staff.give_currency";
}
