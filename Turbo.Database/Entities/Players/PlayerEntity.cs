using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Room;
using Turbo.Database.Entities.Security;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Players;

[Table("players")]
[Index(nameof(NameKey), IsUnique = true)]
public class PlayerEntity : TurboEntity
{
    private string _name = string.Empty;

    /// <summary>The name as the player chose it; setting it sets <see cref="NameKey"/>.</summary>
    [Column("name")]
    public required string Name
    {
        get => _name;
        set
        {
            _name = value;
            NameKey = PlayerNames.Key(value);
        }
    }

    /// <summary>
    /// <see cref="PlayerNames.Key"/> of the name: what makes it unique and what a name is looked
    /// up by, the same on every database whatever its collation.
    /// </summary>
    [Column("name_key")]
    public string NameKey { get; private set; } = string.Empty;

    [Column("motto")]
    public string? Motto { get; set; }

    [Column("figure")]
    [MaxLength(279)]
    [DefaultValue("hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64")]
    public required string Figure { get; set; }

    [Column("gender")]
    [DefaultValue(AvatarGenderType.Male)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required AvatarGenderType Gender { get; set; }

    [Column("status")]
    [DefaultValue(PlayerStatusType.Offline)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required PlayerStatusType PlayerStatus { get; set; }

    [Column("respect_points")]
    [DefaultValue(0)]
    public int RespectPoints { get; set; }

    [Column("respects_left")]
    [DefaultValue(0)]
    public int RespectsLeft { get; set; }

    [Column("pet_respects_left")]
    [DefaultValue(0)]
    public int PetRespectsLeft { get; set; }

    [Column("respect_replenishes_left")]
    [DefaultValue(0)]
    public int RespectReplenishesLeft { get; set; }

    /// <summary>UTC date the daily respect counters were last reset for.</summary>
    [Column("respect_reset_date")]
    public DateTime? RespectResetDate { get; set; }

    /// <summary>When the player last opened a session (UTC); null for one who never has.</summary>
    /// <summary>
    /// The new user flow's look and name step is still to do (<c>Turbo:Onboarding</c>). Set when
    /// the player is created with the flow on; cleared when they choose their name.
    /// </summary>
    [Column("onboarding_name_pending")]
    [DefaultValue(false)]
    public bool OnboardingNamePending { get; set; }

    /// <summary>The new user flow's starter room step is still to do.</summary>
    [Column("onboarding_room_pending")]
    [DefaultValue(false)]
    public bool OnboardingRoomPending { get; set; }

    /// <summary>
    /// A system or service account: one the hotel or a plugin acts as, not a person. It stays out
    /// of what players are shown of other players: the online count, the friend search and the
    /// leaderboards. Set in the admin panel.
    /// </summary>
    [Column("is_system_account")]
    [DefaultValue(false)]
    public bool IsSystemAccount { get; set; }

    /// <summary>
    /// Habbo's account safety lock (<c>UserObject.accountSafetyLocked</c>): while set, the client
    /// turns off buying, selling and trading and the hotel refuses them. Set and cleared through
    /// <c>IPlayerSafetyLockService</c>.
    /// </summary>
    [Column("safety_locked")]
    [DefaultValue(false)]
    public bool SafetyLocked { get; set; }

    [Column("last_login")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// The player's time online in all, in milliseconds: each session's, added as it goes (every
    /// <c>Turbo:Players:AchievementOnlineIntervalSeconds</c> and at its end). Counted from the
    /// version that added it.
    /// </summary>
    [Column("online_ms")]
    [DefaultValue(0L)]
    public long OnlineMilliseconds { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<PlayerBadgeEntity>? PlayerBadges { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<PlayerEffectEntity>? PlayerEffects { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<PlayerCurrencyEntity>? PlayerCurrencies { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<PlayerOutfitEntity>? PlayerOutfits { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<PlayerChatStyleOwnedEntity>? PlayerOwnedChatStyles { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<FurnitureEntity>? Furniture { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerCategoryEntity>? MessengerCategories { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerFriendEntity>? MessengerFriends { get; set; }

    [InverseProperty("RequestedPlayerEntity")]
    public List<MessengerRequestEntity>? MessengerRequests { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerRequestEntity>? MessengerRequestsSent { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<SecurityTicketEntity>? SecurityTickets { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomEntity>? Rooms { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomBanEntity>? RoomBans { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomMuteEntity>? RoomMutes { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomRightEntity>? RoomRights { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomChatlogEntity>? RoomChatlogs { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerBlockedEntity>? MessengerBlocked { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerIgnoredEntity>? MessengerIgnored { get; set; }

    [InverseProperty("SenderPlayerEntity")]
    public List<MessengerMessageEntity>? MessengerMessagesSent { get; set; }

    [InverseProperty("ReceiverPlayerEntity")]
    public List<MessengerMessageEntity>? MessengerMessagesReceived { get; set; }

    /// <summary>When the row last changed; MySQL sets it on every update.</summary>
    [Column("updated_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdatedAt { get; set; }
}
