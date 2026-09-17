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
    public const int DEFAULT_ONLINE_INDICATOR_PREFERENCE = 0;
    public const int DEFAULT_WIRED_VARIABLE_SYNTAX_MODE = 1;
    public const bool DEFAULT_WIRED_SHOW_ALL_NOTIFICATIONS = true;
    public const string DEFAULT_WIRED_UI_STYLE = "";
    public const int WIRED_UI_STYLE_MAX_LENGTH = 50;
    public const int DEFAULT_NAVIGATOR_WINDOW_X = 427;
    public const int DEFAULT_NAVIGATOR_WINDOW_Y = 41;
    public const int DEFAULT_NAVIGATOR_WINDOW_WIDTH = 425;
    public const int DEFAULT_NAVIGATOR_WINDOW_HEIGHT = 400;
    public const NavigatorViewModeType DEFAULT_NAVIGATOR_RESULTS_MODE = NavigatorViewModeType.Tiles;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("generic_volume")]
    [DefaultValue(DEFAULT_VOLUME)]
    public int GenericVolume { get; set; } = DEFAULT_VOLUME;

    [Column("furni_volume")]
    [DefaultValue(DEFAULT_VOLUME)]
    public int FurniVolume { get; set; } = DEFAULT_VOLUME;

    [Column("trax_volume")]
    [DefaultValue(DEFAULT_VOLUME)]
    public int TraxVolume { get; set; } = DEFAULT_VOLUME;

    [Column("room_invites_ignored")]
    [DefaultValue(false)]
    public bool RoomInvitesIgnored { get; set; }

    [Column("room_camera_follow_disabled")]
    [DefaultValue(false)]
    public bool RoomCameraFollowDisabled { get; set; }

    [Column("ui_flags")]
    [DefaultValue(DEFAULT_UI_FLAGS)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public UIFlags UIFlags { get; set; } = DEFAULT_UI_FLAGS;

    [Column("chat_style_id")]
    [DefaultValue(DEFAULT_CHAT_STYLE_ID)]
    public int ChatStyleId { get; set; } = DEFAULT_CHAT_STYLE_ID;

    [Column("chat_font_size")]
    [DefaultValue(DEFAULT_CHAT_FONT_SIZE)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public ChatSizeType ChatFontSize { get; set; } = DEFAULT_CHAT_FONT_SIZE;

    [Column("chat_mode")]
    [DefaultValue(DEFAULT_CHAT_MODE)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public ChatModeType ChatMode { get; set; } = DEFAULT_CHAT_MODE;

    [Column("chat_bubble_width")]
    [DefaultValue(DEFAULT_CHAT_BUBBLE_WIDTH)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public ChatBubbleWidthType ChatBubbleWidth { get; set; } = DEFAULT_CHAT_BUBBLE_WIDTH;

    [Column("chat_scroll_speed")]
    [DefaultValue(DEFAULT_CHAT_SCROLL_SPEED)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public ChatScrollSpeedType ChatScrollSpeed { get; set; } = DEFAULT_CHAT_SCROLL_SPEED;

    [Column("online_indicator_preference")]
    [DefaultValue(DEFAULT_ONLINE_INDICATOR_PREFERENCE)]
    public int OnlineIndicatorPreference { get; set; } = DEFAULT_ONLINE_INDICATOR_PREFERENCE;

    [Column("wired_menu_button")]
    [DefaultValue(false)]
    public bool WiredMenuButton { get; set; }

    [Column("wired_inspect_button")]
    [DefaultValue(false)]
    public bool WiredInspectButton { get; set; }

    [Column("wired_play_test_mode")]
    [DefaultValue(false)]
    public bool WiredPlayTestMode { get; set; }

    [Column("wired_variable_syntax_mode")]
    [DefaultValue(DEFAULT_WIRED_VARIABLE_SYNTAX_MODE)]
    public int WiredVariableSyntaxMode { get; set; } = DEFAULT_WIRED_VARIABLE_SYNTAX_MODE;

    [Column("wired_whisper_disabled")]
    [DefaultValue(false)]
    public bool WiredWhisperDisabled { get; set; }

    [Column("wired_show_all_notifications")]
    [DefaultValue(DEFAULT_WIRED_SHOW_ALL_NOTIFICATIONS)]
    public bool WiredShowAllNotifications { get; set; } = DEFAULT_WIRED_SHOW_ALL_NOTIFICATIONS;

    [Column("wired_ui_style")]
    [MaxLength(WIRED_UI_STYLE_MAX_LENGTH)]
    [DefaultValue(DEFAULT_WIRED_UI_STYLE)]
    public string WiredUIStyle { get; set; } = DEFAULT_WIRED_UI_STYLE;

    [Column("home_room_id")]
    public int? HomeRoomId { get; set; }

    [Column("navigator_window_x")]
    [DefaultValue(DEFAULT_NAVIGATOR_WINDOW_X)]
    public int NavigatorWindowX { get; set; } = DEFAULT_NAVIGATOR_WINDOW_X;

    [Column("navigator_window_y")]
    [DefaultValue(DEFAULT_NAVIGATOR_WINDOW_Y)]
    public int NavigatorWindowY { get; set; } = DEFAULT_NAVIGATOR_WINDOW_Y;

    [Column("navigator_window_width")]
    [DefaultValue(DEFAULT_NAVIGATOR_WINDOW_WIDTH)]
    public int NavigatorWindowWidth { get; set; } = DEFAULT_NAVIGATOR_WINDOW_WIDTH;

    [Column("navigator_window_height")]
    [DefaultValue(DEFAULT_NAVIGATOR_WINDOW_HEIGHT)]
    public int NavigatorWindowHeight { get; set; } = DEFAULT_NAVIGATOR_WINDOW_HEIGHT;

    [Column("navigator_left_pane_hidden")]
    [DefaultValue(false)]
    public bool NavigatorLeftPaneHidden { get; set; }

    [Column("navigator_results_mode")]
    [DefaultValue(DEFAULT_NAVIGATOR_RESULTS_MODE)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public NavigatorViewModeType NavigatorResultsMode { get; set; } =
        DEFAULT_NAVIGATOR_RESULTS_MODE;

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
