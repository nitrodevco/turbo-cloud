using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Settings;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Primitives.Rooms;

namespace Turbo.Players.Grains.Settings;

/// <summary>
/// Owns a player's account preferences (sound, chat, UI flags, room invite/camera toggles, wired
/// editor preferences). Mutations are applied in memory immediately and flushed to the database
/// on a timer and on deactivation, so bursts of preference changes do not each block the grain
/// turn on a DB write.
/// </summary>
internal sealed class PlayerSettingsGrain : Grain, IPlayerSettingsGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PlayerConfig _playerConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerSettingsGrain> _logger;

    private readonly PlayerId _playerId;
    private PlayerSettingsSnapshot _settings = FromEntity(
        new PlayerSettingsEntity { PlayerEntityId = 0 }
    );
    private bool _isDirty;
    private IDisposable? _flushTimer;

    public PlayerSettingsGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        ILogger<IPlayerSettingsGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _playerConfig = playerConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _playerId = this.GetPlayerId();
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate settings for player {PlayerId}", _playerId);

            throw;
        }

        _flushTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((PlayerSettingsGrain)self!).FlushAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_playerConfig.SettingsFlushMs),
            TimeSpan.FromMilliseconds(_playerConfig.SettingsFlushMs)
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _flushTimer?.Dispose();
        _flushTimer = null;

        await FlushAsync(ct);
    }

    public Task<PlayerSettingsSnapshot> GetSettingsAsync(CancellationToken ct) =>
        Task.FromResult(_settings);

    public Task SetSoundSettingsAsync(
        int genericVolume,
        int furniVolume,
        int traxVolume,
        CancellationToken ct
    )
    {
        if (
            !IsValidVolume(genericVolume)
            || !IsValidVolume(furniVolume)
            || !IsValidVolume(traxVolume)
        )
        {
            _logger.LogWarning(
                "Rejected sound settings for player {PlayerId}: volumes {Generic}/{Furni}/{Trax} outside {Min}..{Max}",
                _playerId,
                genericVolume,
                furniVolume,
                traxVolume,
                PlayerSettingsEntity.VOLUME_MIN,
                PlayerSettingsEntity.VOLUME_MAX
            );

            return Task.CompletedTask;
        }

        Apply(
            _settings with
            {
                GenericVolume = genericVolume,
                FurniVolume = furniVolume,
                TraxVolume = traxVolume,
            }
        );

        return Task.CompletedTask;
    }

    public Task SetChatPreferencesAsync(
        ChatModeType chatMode,
        ChatBubbleWidthType bubbleWidth,
        ChatScrollSpeedType scrollSpeed,
        CancellationToken ct
    )
    {
        if (
            !Enum.IsDefined(chatMode)
            || !Enum.IsDefined(bubbleWidth)
            || !Enum.IsDefined(scrollSpeed)
        )
        {
            _logger.LogWarning(
                "Rejected chat preferences for player {PlayerId}: mode {ChatMode}, bubble width {BubbleWidth}, scroll speed {ScrollSpeed}",
                _playerId,
                chatMode,
                bubbleWidth,
                scrollSpeed
            );

            return Task.CompletedTask;
        }

        Apply(
            _settings with
            {
                ChatMode = chatMode,
                ChatBubbleWidth = bubbleWidth,
                ChatScrollSpeed = scrollSpeed,
            }
        );

        return Task.CompletedTask;
    }

    public Task SetChatStyleAsync(int chatStyleId, ChatSizeType fontSize, CancellationToken ct)
    {
        if (chatStyleId < 0 || !Enum.IsDefined(fontSize))
        {
            _logger.LogWarning(
                "Rejected chat style for player {PlayerId}: style {ChatStyleId}, font size {FontSize}",
                _playerId,
                chatStyleId,
                fontSize
            );

            return Task.CompletedTask;
        }

        Apply(_settings with { ChatStyleId = chatStyleId, ChatFontSize = fontSize });

        return Task.CompletedTask;
    }

    public Task SetIgnoreRoomInvitesAsync(bool ignoreRoomInvites, CancellationToken ct)
    {
        Apply(_settings with { RoomInvitesIgnored = ignoreRoomInvites });

        return Task.CompletedTask;
    }

    public Task SetRoomCameraFollowDisabledAsync(bool cameraFollowDisabled, CancellationToken ct)
    {
        Apply(_settings with { RoomCameraFollowDisabled = cameraFollowDisabled });

        return Task.CompletedTask;
    }

    public Task SetUIFlagsAsync(UIFlags uiFlags, CancellationToken ct)
    {
        Apply(_settings with { UIFlags = uiFlags });

        return Task.CompletedTask;
    }

    public Task SetWiredPreferencesAsync(
        bool menuButton,
        bool inspectButton,
        bool playTestMode,
        int variableSyntaxMode,
        bool whisperDisabled,
        bool showAllNotifications,
        string uiStyle,
        CancellationToken ct
    )
    {
        if (
            variableSyntaxMode < 0
            || uiStyle is null
            || uiStyle.Length > PlayerSettingsEntity.WIRED_UI_STYLE_MAX_LENGTH
        )
        {
            _logger.LogWarning(
                "Rejected wired preferences for player {PlayerId}: variable syntax mode {VariableSyntaxMode}, ui style length {UIStyleLength} (max {MaxLength})",
                _playerId,
                variableSyntaxMode,
                uiStyle?.Length ?? -1,
                PlayerSettingsEntity.WIRED_UI_STYLE_MAX_LENGTH
            );

            return Task.CompletedTask;
        }

        Apply(
            _settings with
            {
                WiredMenuButton = menuButton,
                WiredInspectButton = inspectButton,
                WiredPlayTestMode = playTestMode,
                WiredVariableSyntaxMode = variableSyntaxMode,
                WiredWhisperDisabled = whisperDisabled,
                WiredShowAllNotifications = showAllNotifications,
                WiredUIStyle = uiStyle,
            }
        );

        return Task.CompletedTask;
    }

    public async Task SetHomeRoomAsync(RoomId roomId, CancellationToken ct)
    {
        var homeRoomId = roomId.Value > 0 ? roomId : RoomId.Invalid;

        Apply(_settings with { HomeRoomId = homeRoomId });

        await _grainFactory
            .GetPlayerPresenceGrain(_playerId)
            .SendComposerAsync(
                new NavigatorSettingsMessageComposer
                {
                    HomeRoomId = homeRoomId,
                    RoomIdToEnter = RoomId.Invalid,
                },
                ct
            );
    }

    public Task SetNavigatorWindowPreferencesAsync(
        int x,
        int y,
        int width,
        int height,
        bool leftPaneHidden,
        NavigatorViewModeType resultsMode,
        CancellationToken ct
    )
    {
        if (width <= 0 || height <= 0 || !Enum.IsDefined(resultsMode))
        {
            _logger.LogWarning(
                "Rejected navigator window preferences for player {PlayerId}: size {Width}x{Height}, results mode {ResultsMode}",
                _playerId,
                width,
                height,
                resultsMode
            );

            return Task.CompletedTask;
        }

        Apply(
            _settings with
            {
                NavigatorWindowX = x,
                NavigatorWindowY = y,
                NavigatorWindowWidth = width,
                NavigatorWindowHeight = height,
                NavigatorLeftPaneHidden = leftPaneHidden,
                NavigatorResultsMode = resultsMode,
            }
        );

        return Task.CompletedTask;
    }

    private static bool IsValidVolume(int volume) =>
        volume is >= PlayerSettingsEntity.VOLUME_MIN and <= PlayerSettingsEntity.VOLUME_MAX;

    private void Apply(PlayerSettingsSnapshot next)
    {
        if (next == _settings)
            return;

        _settings = next;
        _isDirty = true;
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await dbCtx
            .PlayerSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlayerEntityId == _playerId.Value, ct);

        // A missing row means the player has never changed anything; the entity's property
        // initializers are the single source of the defaults.
        _settings = FromEntity(
            entity ?? new PlayerSettingsEntity { PlayerEntityId = _playerId.Value }
        );
        _isDirty = false;
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        if (!_isDirty)
            return;

        var settings = _settings;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx.PlayerSettings.FirstOrDefaultAsync(
                x => x.PlayerEntityId == _playerId.Value,
                ct
            );

            if (entity is null)
            {
                entity = new PlayerSettingsEntity { PlayerEntityId = _playerId.Value };

                dbCtx.PlayerSettings.Add(entity);
            }

            ApplyTo(entity, settings);

            await dbCtx.SaveChangesAsync(ct);

            // Only clear the dirty flag if nothing changed while the write was in flight.
            if (ReferenceEquals(settings, _settings))
                _isDirty = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush settings for player {PlayerId}", _playerId);
        }
    }

    private static PlayerSettingsSnapshot FromEntity(PlayerSettingsEntity entity) =>
        new()
        {
            GenericVolume = entity.GenericVolume,
            FurniVolume = entity.FurniVolume,
            TraxVolume = entity.TraxVolume,
            RoomInvitesIgnored = entity.RoomInvitesIgnored,
            RoomCameraFollowDisabled = entity.RoomCameraFollowDisabled,
            UIFlags = entity.UIFlags,
            ChatStyleId = entity.ChatStyleId,
            ChatFontSize = entity.ChatFontSize,
            ChatMode = entity.ChatMode,
            ChatBubbleWidth = entity.ChatBubbleWidth,
            ChatScrollSpeed = entity.ChatScrollSpeed,
            OnlineIndicatorPreference = entity.OnlineIndicatorPreference,
            WiredMenuButton = entity.WiredMenuButton,
            WiredInspectButton = entity.WiredInspectButton,
            WiredPlayTestMode = entity.WiredPlayTestMode,
            WiredVariableSyntaxMode = entity.WiredVariableSyntaxMode,
            WiredWhisperDisabled = entity.WiredWhisperDisabled,
            WiredShowAllNotifications = entity.WiredShowAllNotifications,
            WiredUIStyle = entity.WiredUIStyle,
            HomeRoomId = entity.HomeRoomId is > 0 ? entity.HomeRoomId.Value : RoomId.Invalid,
            NavigatorWindowX = entity.NavigatorWindowX,
            NavigatorWindowY = entity.NavigatorWindowY,
            NavigatorWindowWidth = entity.NavigatorWindowWidth,
            NavigatorWindowHeight = entity.NavigatorWindowHeight,
            NavigatorLeftPaneHidden = entity.NavigatorLeftPaneHidden,
            NavigatorResultsMode = entity.NavigatorResultsMode,
        };

    private static void ApplyTo(PlayerSettingsEntity entity, PlayerSettingsSnapshot settings)
    {
        entity.GenericVolume = settings.GenericVolume;
        entity.FurniVolume = settings.FurniVolume;
        entity.TraxVolume = settings.TraxVolume;
        entity.RoomInvitesIgnored = settings.RoomInvitesIgnored;
        entity.RoomCameraFollowDisabled = settings.RoomCameraFollowDisabled;
        entity.UIFlags = settings.UIFlags;
        entity.ChatStyleId = settings.ChatStyleId;
        entity.ChatFontSize = settings.ChatFontSize;
        entity.ChatMode = settings.ChatMode;
        entity.ChatBubbleWidth = settings.ChatBubbleWidth;
        entity.ChatScrollSpeed = settings.ChatScrollSpeed;
        entity.OnlineIndicatorPreference = settings.OnlineIndicatorPreference;
        entity.WiredMenuButton = settings.WiredMenuButton;
        entity.WiredInspectButton = settings.WiredInspectButton;
        entity.WiredPlayTestMode = settings.WiredPlayTestMode;
        entity.WiredVariableSyntaxMode = settings.WiredVariableSyntaxMode;
        entity.WiredWhisperDisabled = settings.WiredWhisperDisabled;
        entity.WiredShowAllNotifications = settings.WiredShowAllNotifications;
        entity.WiredUIStyle = settings.WiredUIStyle;
        entity.HomeRoomId = settings.HomeRoomId.Value > 0 ? settings.HomeRoomId.Value : null;
        entity.NavigatorWindowX = settings.NavigatorWindowX;
        entity.NavigatorWindowY = settings.NavigatorWindowY;
        entity.NavigatorWindowWidth = settings.NavigatorWindowWidth;
        entity.NavigatorWindowHeight = settings.NavigatorWindowHeight;
        entity.NavigatorLeftPaneHidden = settings.NavigatorLeftPaneHidden;
        entity.NavigatorResultsMode = settings.NavigatorResultsMode;
    }
}
