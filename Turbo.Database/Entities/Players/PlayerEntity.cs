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

[Comment("The hotel's players.")]
[Table("players")]
[Index(nameof(NameKey), IsUnique = true)]
public class PlayerEntity : TurboEntity
{
    /// <summary>
    /// The longest figure a player, a saved outfit or a bot may wear: every set type with two
    /// colours. One limit, so a look a player wears can always be saved and given to a bot.
    /// </summary>
    public const int FIGURE_MAX_LENGTH = 279;

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
    [DefaultValue("")]
    public string Motto { get; set; } = string.Empty;

    [Column("figure")]
    [MaxLength(FIGURE_MAX_LENGTH)]
    [DefaultValue("hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64")]
    public required string Figure { get; set; }

    [Column("gender")]
    [DefaultValue(AvatarGenderType.Male)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required AvatarGenderType Gender { get; set; }

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

    /// <summary>The UTC day the daily respect counters were last reset for.</summary>
    [Column("respect_reset_date")]
    public DateOnly? RespectResetDate { get; set; }

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

    /// <summary>When the player last opened a session (UTC); null for one who never has.</summary>
    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// When the player was last online: the end of their last session. Kept here as well as in
    /// <c>player_logins</c>, which old rows are purged from, so "last seen" outlives the log.
    /// </summary>
    [Column("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

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
    public List<MessengerBlockedEntity>? MessengerBlocked { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerIgnoredEntity>? MessengerIgnored { get; set; }
}
