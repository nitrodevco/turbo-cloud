using Turbo.Primitives.Vault.Enums;

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

    /// <summary>
    /// An earnings vault payout of a category this list does not name. A payout of a named one
    /// carries its category's own code (<see cref="VaultClaimOf"/>).
    /// </summary>
    public const string VAULT_CLAIM = "vault.claim";

    // One code per earnings category, named as Flash's EarningsView names them, so each source
    // the vault pays out is a cause of its own.
    public const string VAULT_TUTORIAL = "vault.tutorial";
    public const string VAULT_DAILY_GIFT = "vault.dailygift";
    public const string VAULT_ACHIEVEMENTS = "vault.achievements";
    public const string VAULT_MARKETPLACE = "vault.marketplace";
    public const string VAULT_HABBO_CLUB = "vault.habboclub";
    public const string VAULT_LEVEL_PROGRESSION = "vault.levelprogression";
    public const string VAULT_ROOM_BUNDLE_SALES = "vault.roombundlesales";
    public const string VAULT_BONUS_BAG = "vault.bonusbag";
    public const string VAULT_DONATION = "vault.donation";
    public const string VAULT_SURPRISE = "vault.surprise";
    public const string VAULT_SNOWSTORM = "vault.snowstorm";
    public const string VAULT_GAMES = "vault.games";
    public const string VAULT_WIRED_CHEST = "vault.wiredchest";
    public const string VAULT_AGENCY = "vault.agency";

    public const string MARKET_LIST = "market.list";
    public const string MARKET_TOKENS = "market.tokens";
    public const string MARKET_CANCEL = "market.cancel";
    public const string MARKET_BUY = "market.buy";

    /// <summary>
    /// The marketplace's fee on a sale: the share of the price that leaves the offer when it is
    /// sold, in the sale's operation, so the seller is paid the rest.
    /// </summary>
    public const string MARKET_FEE = "market.fee";
    public const string MARKET_REDEEM = "market.redeem";

    /// <summary>A trade's operation; its cause ref is the room the trade was made in.</summary>
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

    /// <summary>A new player's pick of the new user gift offer; cause ref: the step, <c>nux:day:step</c>.</summary>
    public const string ONBOARDING_GIFT = "onboarding.gift";

    public const string REWARD_TRACK = "reward.track";

    /// <summary>A reward track's premium bought: its price, apart from the prizes the track pays.</summary>
    public const string REWARD_TRACK_PREMIUM = "reward.track_premium";
    public const string REWARD_QUEST = "reward.quest";

    /// <summary>A campaign calendar door opened; cause ref: the door, <c>calendar:campaign:day</c>.</summary>
    public const string CAMPAIGN_CALENDAR = "reward.campaign_calendar";

    public const string REWARD_DAILY_TASK = "reward.daily_task";
    public const string REWARD_ACHIEVEMENT = "reward.achievement";

    /// <summary>Currency paid for time online (<c>Turbo:OnlineRewards</c>); cause ref: the currency.</summary>
    public const string REWARD_ONLINE = "reward.online";

    public const string STAFF_GIVE_CURRENCY = "staff.give_currency";
    public const string STAFF_GIVE_ITEM = "staff.give_item";
    public const string STAFF_GIFT = "staff.gift";
    public const string STAFF_DELETE_BOT = "staff.delete_bot";

    /// <summary>
    /// A movement undone through <see cref="IAssetReversalService"/>: its cause ref is the
    /// operation it undoes, its actor whom the reversal was asked for.
    /// </summary>
    public const string REVERSAL = "reversal";

    /// <summary>The code of an earnings vault payout of <paramref name="category"/>.</summary>
    public static string VaultClaimOf(VaultRewardCategoryType category) =>
        category switch
        {
            VaultRewardCategoryType.Tutorial => VAULT_TUTORIAL,
            VaultRewardCategoryType.DailyGift => VAULT_DAILY_GIFT,
            VaultRewardCategoryType.Achievements => VAULT_ACHIEVEMENTS,
            VaultRewardCategoryType.Marketplace => VAULT_MARKETPLACE,
            VaultRewardCategoryType.HabboClub => VAULT_HABBO_CLUB,
            VaultRewardCategoryType.LevelProgression => VAULT_LEVEL_PROGRESSION,
            VaultRewardCategoryType.RoomBundleSales => VAULT_ROOM_BUNDLE_SALES,
            VaultRewardCategoryType.BonusBag => VAULT_BONUS_BAG,
            VaultRewardCategoryType.Donation => VAULT_DONATION,
            VaultRewardCategoryType.Suprise => VAULT_SURPRISE,
            VaultRewardCategoryType.SnowStorm => VAULT_SNOWSTORM,
            VaultRewardCategoryType.Games => VAULT_GAMES,
            VaultRewardCategoryType.WiredChest => VAULT_WIRED_CHEST,
            VaultRewardCategoryType.Agency => VAULT_AGENCY,
            _ => VAULT_CLAIM,
        };
}
