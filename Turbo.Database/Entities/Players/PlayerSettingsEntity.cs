using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Database.Entities.Players;

[Table("player_settings")]
[Index(nameof(PlayerEntityId), IsUnique = true)]
public class PlayerSettingsEntity : TurboEntity
{
    public const int VOLUME_MIN = 0;
    public const int VOLUME_MAX = 100;
    public const int DEFAULT_VOLUME = 100;
    public const int DEFAULT_CHAT_STYLE_ID = 0;
    public const UIFlags DEFAULT_UI_FLAGS = UIFlags.FriendBarExpanded | UIFlags.RoomToolsExpanded;
    public const ChatSizeType DEFAULT_CHAT_FONT_SIZE = ChatSizeType.Zero;
    public const ChatModeType DEFAULT_CHAT_MODE = ChatModeType.FreeFlow;
    public const ChatBubbleWidthType DEFAULT_CHAT_BUBBLE_WIDTH = ChatBubbleWidthType.Normal;
    public const ChatScrollSpeedType DEFAULT_CHAT_SCROLL_SPEED = ChatScrollSpeedType.Normal;
    public const int DEFAULT_WIRED_VARIABLE_SYNTAX_MODE = 1;
    public const bool DEFAULT_WIRED_SHOW_ALL_NOTIFICATIONS = true;
    public const string DEFAULT_WIRED_UI_STYLE = "";
    public const int WIRED_UI_STYLE_MAX_LENGTH = 50;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("generic_volume")]
    [DefaultValue(DEFAULT_VOLUME)]
    public required int GenericVolume { get; set; }

    [Column("furni_volume")]
    [DefaultValue(DEFAULT_VOLUME)]
    public required int FurniVolume { get; set; }

    [Column("trax_volume")]
    [DefaultValue(DEFAULT_VOLUME)]
    public required int TraxVolume { get; set; }

    [Column("room_invites_ignored")]
    [DefaultValue(false)]
    public required bool RoomInvitesIgnored { get; set; }

    [Column("room_camera_follow_disabled")]
    [DefaultValue(false)]
    public required bool RoomCameraFollowDisabled { get; set; }

    [Column("ui_flags")]
    [DefaultValue(DEFAULT_UI_FLAGS)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required UIFlags UIFlags { get; set; }

    [Column("chat_style_id")]
    [DefaultValue(DEFAULT_CHAT_STYLE_ID)]
    public required int ChatStyleId { get; set; }

    [Column("chat_font_size")]
    [DefaultValue(DEFAULT_CHAT_FONT_SIZE)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ChatSizeType ChatFontSize { get; set; }

    [Column("chat_mode")]
    [DefaultValue(DEFAULT_CHAT_MODE)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ChatModeType ChatMode { get; set; }

    [Column("chat_bubble_width")]
    [DefaultValue(DEFAULT_CHAT_BUBBLE_WIDTH)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ChatBubbleWidthType ChatBubbleWidth { get; set; }

    [Column("chat_scroll_speed")]
    [DefaultValue(DEFAULT_CHAT_SCROLL_SPEED)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ChatScrollSpeedType ChatScrollSpeed { get; set; }

    [Column("wired_menu_button")]
    [DefaultValue(false)]
    public required bool WiredMenuButton { get; set; }

    [Column("wired_inspect_button")]
    [DefaultValue(false)]
    public required bool WiredInspectButton { get; set; }

    [Column("wired_play_test_mode")]
    [DefaultValue(false)]
    public required bool WiredPlayTestMode { get; set; }

    [Column("wired_variable_syntax_mode")]
    [DefaultValue(DEFAULT_WIRED_VARIABLE_SYNTAX_MODE)]
    public required int WiredVariableSyntaxMode { get; set; }

    [Column("wired_whisper_disabled")]
    [DefaultValue(false)]
    public required bool WiredWhisperDisabled { get; set; }

    [Column("wired_show_all_notifications")]
    [DefaultValue(DEFAULT_WIRED_SHOW_ALL_NOTIFICATIONS)]
    public required bool WiredShowAllNotifications { get; set; }

    [Column("wired_ui_style")]
    [MaxLength(WIRED_UI_STYLE_MAX_LENGTH)]
    [DefaultValue(DEFAULT_WIRED_UI_STYLE)]
    public required string WiredUIStyle { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
