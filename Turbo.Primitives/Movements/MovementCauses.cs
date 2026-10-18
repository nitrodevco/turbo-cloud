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

    /// <summary>
    /// The marker pruning leaves in place of the newest row it removed: every id up to and
    /// including its own is gone. The dispatcher reports it to a consumer as an
    /// <see cref="OutboxGap"/> and never delivers it as a row.
    /// </summary>
    public const string OUTBOX_PRUNED = "outbox.pruned";

    public const string CATALOG_PURCHASE = "catalog.purchase";
    public const string CATALOG_GIFT = "catalog.gift";
    public const string CATALOG_CLUB_GIFT = "catalog.club_gift";
    public const string CATALOG_ROOM_AD = "catalog.room_ad";
    public const string CATALOG_TARGETED_OFFER = "catalog.targeted_offer";
    public const string CATALOG_LTD_RAFFLE = "catalog.ltd_raffle";
    public const string CATALOG_BONUS_RARE = "catalog.bonus_rare";
    public const string CATALOG_VOUCHER = "catalog.voucher";
    public const string CATALOG_HABBICON = "catalog.habbicon";
    public const string CAMERA_PURCHASE = "camera.purchase";
    public const string CAMERA_PUBLISH = "camera.publish";
    public const string GROUP_CREATE = "group.create";
    public const string VAULT_CLAIM = "vault.claim";

    public const string MARKET_LIST = "market.list";
    public const string MARKET_TOKENS = "market.tokens";
    public const string MARKET_CANCEL = "market.cancel";
    public const string MARKET_BUY = "market.buy";
    public const string MARKET_REDEEM = "market.redeem";

    public const string TRADE = "trade";

    /// <summary>A trade's cause ref on the movements that give back the first half after the second failed.</summary>
    public const string TRADE_REF_COMPENSATION = "compensation";

    public const string ROOM_PLACE = "room.place";
    public const string ROOM_PICKUP = "room.pickup";
    public const string ROOM_PICKALL = "room.pickall";
    public const string ROOM_EJECTALL = "room.ejectall";
    public const string ROOM_DELETE = "room.delete";
    public const string ROOM_FLOORPLAN = "room.floorplan";
    public const string ROOM_RENT = "room.rent";
    public const string ROOM_RENT_END = "room.rent_end";
    public const string ROOM_DELETE_ITEM = "room.delete_item";
    public const string GROUP_FURNI_REMOVED = "group.furni_removed";
    public const string ROOM_GIVE_BACK_USED = "room.give_back_used";
    public const string ROOM_BC_BORROW = "room.bc_borrow";
    public const string ROOM_POST_IT = "room.post_it";
    public const string USE_CREDIT_FURNI = "use.credit_furni";
    public const string USE_CRACKABLE = "use.crackable";
    public const string USE_PRESENT = "use.present";
    public const string USE_CRAFTING = "use.crafting";
    public const string USE_DECORATION = "use.decoration";
    public const string USE_EFFECT_BOX = "use.effect_box";
    public const string USE_CLOTHING = "use.clothing";
    public const string USE_PET_SUPPLY = "use.pet_supply";
    public const string USE_PET_PRODUCT = "use.pet_product";
    public const string USE_PET_PACKAGE = "use.pet_package";
    public const string USE_MONSTERPLANT_SEED = "use.monsterplant_seed";

    public const string PET_BREED = "pet.breed";
    public const string PET_HARVEST = "pet.harvest";
    public const string PET_PLACE = "pet.place";
    public const string PET_PICKUP = "pet.pickup";
    public const string PET_UNSADDLE = "pet.unsaddle";
    public const string PET_COMPOST = "pet.compost";
    public const string BOT_PLACE = "bot.place";
    public const string BOT_PICKUP = "bot.pickup";
    public const string PRODUCT_CLAIM = "product.claim";

    public const string WIRED_GIVE_REWARD = "wired.give_reward";

    public const string WIRED_CHEST_UPGRADE = "wired.chest_upgrade";
    public const string WIRED_CHEST_DEPOSIT = "wired.chest_deposit";
    public const string WIRED_CHEST_WITHDRAW = "wired.chest_withdraw";

    public const string JUKEBOX_ADD = "jukebox.add";
    public const string JUKEBOX_REMOVE = "jukebox.remove";

    public const string ONBOARDING_STARTER_ROOM = "onboarding.starter_room";

    public const string REWARD_TRACK = "reward.track";
    public const string REWARD_QUEST = "reward.quest";
    public const string REWARD_DAILY_TASK = "reward.daily_task";
    public const string REWARD_ACHIEVEMENT = "reward.achievement";

    public const string STAFF_GIVE_CURRENCY = "staff.give_currency";
    public const string STAFF_GIVE_ITEM = "staff.give_item";
    public const string STAFF_GIFT = "staff.gift";
    public const string STAFF_DELETE_BOT = "staff.delete_bot";
}
