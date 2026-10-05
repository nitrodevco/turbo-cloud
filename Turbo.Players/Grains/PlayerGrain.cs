using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Events;
using Turbo.Logging;
using Turbo.Players.Configuration;
using Turbo.Primitives;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Texts;

namespace Turbo.Players.Grains;

/// <summary>
/// A player's profile. Write-through: every change is saved as it happens, and deactivation
/// saves once more so whatever changed last is not lost.
/// </summary>
internal sealed partial class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PlayerConfig _playerConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerGrain> _logger;
    private readonly IAchievementFactRecorder _achievementFacts;
    private readonly EventSystem _eventSystem;

    private readonly PlayerLiveState _state;

    public PlayerId PlayerId => _state.PlayerId;

    public PlayerGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        IAchievementFactRecorder achievementFacts,
        EventSystem eventSystem,
        ILogger<IPlayerGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _playerConfig = playerConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;
        _achievementFacts = achievementFacts;
        _eventSystem = eventSystem;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate player {PlayerId}", _state.PlayerId);

            throw;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        // Nothing can act on a failure here, but losing the last changes must not be silent.
        try
        {
            await WriteToDatabaseAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save player {PlayerId} on deactivation",
                _state.PlayerId
            );
        }
    }

    public async Task SetOnlineStatusAsync(bool flag, CancellationToken ct)
    {
        // A session opening is a login: the profile's "last login" counts from it.
        if (flag)
            await RecordLoginAsync(ct);
        _state.IsOnline = flag;

        // A temporary look belongs to the session: it ends with it, and the update below tells
        // the room (while the player is still in it) and friends the saved look is back.
        if (!flag)
            ReleaseLookOverride();

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(PlayerId);

        await playerPresence.OnPlayerUpdatedAsync(await GetSummaryAsync(ct), ct);
    }

    public async Task SetFigureAsync(string figure, AvatarGenderType gender, CancellationToken ct)
    {
        if (_state.Figure == figure && _state.Gender == gender)
            return;
        var previousFigure = _state.Figure;
        var previousGender = _state.Gender;
        _state.Figure = figure;
        _state.Gender = gender;

        try
        {
            await WriteToDatabaseAsync(
                ct,
                previousFigure != figure ? AchievementSources.FIGURE : null
            );
        }
        catch (Exception ex)
        {
            _state.Figure = previousFigure;
            _state.Gender = previousGender;
            _logger.LogError(ex, "Failed to change figure for player {PlayerId}", PlayerId);
            throw;
        }

        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            // The look the player is shown with, which is the override's while one is set: the
            // saved figure is what comes back when it is cleared.
            ToFigureUpdate(),
            ct
        );

        // Whoever else sees the player (their room, their friends) hears through the presence,
        // which knows where they are.
        await _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .OnPlayerUpdatedAsync(await GetSummaryAsync(ct), ct);
    }

    public async Task<bool> SetLookOverrideAsync(
        string figure,
        AvatarGenderType? gender,
        LookOverrideMode mode,
        CancellationToken ct
    )
    {
        if (!FigureString.IsWellFormed(figure) || !Enum.IsDefined(mode))
        {
            _logger.LogWarning(
                "Rejected look override for player {PlayerId}: the figure or mode is not valid",
                PlayerId
            );
            return false;
        }

        // The override ends with the session, so one set with no session would never end.
        if (!await _grainFactory.GetPlayerPresenceGrain(PlayerId).HasActiveSessionAsync(ct))
        {
            _logger.LogWarning(
                "Rejected look override for player {PlayerId}: no active session",
                PlayerId
            );
            return false;
        }

        var next = new PlayerLookOverrideSnapshot
        {
            Figure = figure,
            Gender = gender,
            Mode = mode,
        };
        if (_state.LookOverride == next)
            return true;

        ApplyLookOverride(next);
        DelayDeactivation(TimeSpan.FromMinutes(_playerConfig.LookOverrideKeepAliveMinutes));

        await ShowLookAsync(ct);

        return true;
    }

    public async Task<bool> ClearLookOverrideAsync(CancellationToken ct)
    {
        if (_state.LookOverride is null)
            return false;

        ReleaseLookOverride();

        await ShowLookAsync(ct);

        return true;
    }

    public Task<PlayerLookOverrideSnapshot?> GetLookOverrideAsync(CancellationToken ct) =>
        Task.FromResult(_state.LookOverride);

    /// <summary>Drops the temporary look, if any, without telling the client or the room.</summary>
    private void ReleaseLookOverride()
    {
        if (_state.LookOverride is null)
            return;

        ApplyLookOverride(null);
        // Back to the default collection age; the grain was only held for the look.
        DelayDeactivation(TimeSpan.Zero);
    }

    private void ApplyLookOverride(PlayerLookOverrideSnapshot? next)
    {
        var previous = _state.LookOverride;
        _state.LookOverride = next;
        var (figure, gender) = PlayerLook.Resolve(_state.Figure, _state.Gender, next);

        // Not awaited: a handler may call back into this grain, which would wait on the call
        // raising it.
        _eventSystem
            .PublishAsync(
                new PlayerLookOverrideChangedEvent
                {
                    PlayerId = PlayerId,
                    Previous = previous,
                    Current = next,
                    Figure = figure,
                    Gender = gender,
                },
                CancellationToken.None
            )
            .LogAndForget(_logger, "announce the look override of player {PlayerId}", PlayerId);
    }

    /// <summary>
    /// Tells the player's own client and whoever sees them (their room, their friends, through
    /// the presence) the look they are shown with now.
    /// </summary>
    private async Task ShowLookAsync(CancellationToken ct)
    {
        await _grainFactory.SendComposerToPlayerAsync(PlayerId, ToFigureUpdate(), ct);

        await _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .OnPlayerUpdatedAsync(await GetSummaryAsync(ct), ct);
    }

    private FigureUpdateEventMessageComposer ToFigureUpdate()
    {
        var (figure, gender) = PlayerLook.Resolve(
            _state.Figure,
            _state.Gender,
            _state.LookOverride
        );

        return new FigureUpdateEventMessageComposer { Figure = figure, Gender = gender };
    }

    public async Task SetMottoAsync(string text, CancellationToken ct)
    {
        if (_state.Motto == text)
            return;
        var previous = _state.Motto;
        _state.Motto = text;

        try
        {
            await WriteToDatabaseAsync(ct, AchievementSources.MOTTO);
        }
        catch (Exception ex)
        {
            _state.Motto = previous;
            _logger.LogError(ex, "Failed to change motto for player {PlayerId}", PlayerId);
            throw;
        }

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(PlayerId);

        await playerPresence.OnPlayerUpdatedAsync(await GetSummaryAsync(ct), ct);
    }

    /// <summary>
    /// Stamps the login time, written at once and alone (the rest of the row is this grain's to
    /// write on its own schedule). Its login fact commits in the same transaction, before
    /// authentication is acknowledged. A failed write aborts this login mutation.
    /// </summary>
    private async Task RecordLoginAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db
            .Players.Where(x => x.Id == PlayerId.Value)
            .ExecuteUpdateAsync(up => up.SetProperty(x => x.LastLoginAt, now), ct);
        if (updated != 1)
            throw new InvalidOperationException(
                "Login did not update its authoritative player row."
            );
        _achievementFacts.Record(
            db,
            PlayerId,
            new()
            {
                Source = AchievementSources.LOGIN,
                OperationId = Guid.NewGuid().ToString("N"),
                OccurredAtUtc = now,
            }
        );
        _achievementFacts.Record(
            db,
            PlayerId,
            new()
            {
                Source = AchievementSources.ACCOUNT_AGE,
                OperationId = Guid.NewGuid().ToString("N"),
                OccurredAtUtc = now,
                Amount = Math.Max(0, (long)(now - _state.CreatedAt).TotalDays),
            }
        );
        _achievementFacts.Record(
            db,
            PlayerId,
            new()
            {
                Source = AchievementSources.PETS,
                OperationId = Guid.NewGuid().ToString("N"),
                OccurredAtUtc = now,
                Amount = await db.Pets.CountAsync(
                    x => x.PlayerEntityId == PlayerId.Value && x.DeletedAt == null,
                    ct
                ),
            }
        );
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _state.LastLoginAtUtc = now;
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity =
            await dbCtx
                .Players.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == (int)_state.PlayerId, ct)
            ?? throw new TurboException(TurboErrorCodeEnum.PlayerNotFound);

        _state.Name = entity.Name;
        _state.Motto = entity.Motto ?? string.Empty;
        _state.Figure = entity.Figure;
        _state.Gender = entity.Gender;
        var achievements = await dbCtx
            .AchievementProjections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PlayerId == _state.PlayerId.Value, ct);
        _state.AchievementScore = achievements?.Score ?? 0;
        _state.AchievementLevel = achievements?.EarnedLevels ?? 0;
        _state.CreatedAt = entity.CreatedAt;
        _state.LastUpdated = entity.UpdatedAt;
        _state.LastLoginAtUtc = entity.LastLoginAt;
        _state.RespectPoints = entity.RespectPoints;
        _state.RespectsLeft = entity.RespectsLeft;
        _state.PetRespectsLeft = entity.PetRespectsLeft;
        _state.RespectReplenishesLeft = entity.RespectReplenishesLeft;
        _state.RespectResetDate = entity.RespectResetDate;

        // Online-ness is not ours to remember: this grain is collected while idle and comes back
        // long before the player logs out, so the session holder is asked instead of guessing.
        //
        // Asked a turn later rather than here, and not awaited. The presence grain is often what
        // activated us — it reads this grain's summary on room entry — and it would then be
        // sitting inside that call waiting for an activation that was waiting for it. The
        // presence pushes this value on every session open and close anyway, so all this
        // recovers is the case of being collected mid-session, and it self-corrects a turn in.
        RefreshOnlineStatusAsync()
            .LogAndForget(_logger, "refresh online status of player {PlayerId}", _state.PlayerId);

        ResetDailyRespectIfDue();

        await _grainFactory.GetPlayerDirectoryGrain().SetPlayerNameAsync(PlayerId, _state.Name, ct);
    }

    private async Task WriteToDatabaseAsync(CancellationToken ct, string? achievementSource = null)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var snapshot = await GetSummaryAsync(ct);
        await using var transaction = achievementSource is null
            ? null
            : await dbCtx.Database.BeginTransactionAsync(ct);

        var updated = await dbCtx
            .Players.Where(x => x.Id == (int)_state.PlayerId)
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(p => p.Name, snapshot.Name)
                        .SetProperty(p => p.Motto, snapshot.Motto)
                        // The saved figure, never the summary's: that one shows a temporary look.
                        .SetProperty(p => p.Figure, _state.Figure)
                        .SetProperty(p => p.Gender, _state.Gender)
                        .SetProperty(p => p.RespectPoints, _state.RespectPoints)
                        .SetProperty(p => p.RespectsLeft, _state.RespectsLeft)
                        .SetProperty(p => p.PetRespectsLeft, _state.PetRespectsLeft)
                        .SetProperty(p => p.RespectReplenishesLeft, _state.RespectReplenishesLeft)
                        .SetProperty(p => p.RespectResetDate, _state.RespectResetDate),
                ct
            );

        if (updated != 1)
            throw new InvalidOperationException(
                "Player mutation did not update its authoritative row."
            );
        if (achievementSource is not null)
        {
            _achievementFacts.Record(
                dbCtx,
                PlayerId,
                new()
                {
                    Source = achievementSource,
                    OperationId = Guid.NewGuid().ToString("N"),
                    OccurredAtUtc = DateTime.UtcNow,
                }
            );
            await dbCtx.SaveChangesAsync(ct);
            await transaction!.CommitAsync(ct);
        }
        _state.LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Figure and gender are the look the player is shown with, so a temporary look reaches
    /// every reader (room entry, live updates, friends) from here.
    /// </summary>
    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct)
    {
        var (figure, gender) = PlayerLook.Resolve(
            _state.Figure,
            _state.Gender,
            _state.LookOverride
        );

        return Task.FromResult(
            new PlayerSummarySnapshot
            {
                PlayerId = _state.PlayerId,
                Name = _state.Name,
                Motto = _state.Motto,
                Figure = figure,
                Gender = gender,
                AchievementScore = _state.AchievementScore,
                BadgesRank = _state.BadgesRank,
                IsOnline = _state.IsOnline,
                CreatedAt = _state.CreatedAt,
                LastUpdated = _state.LastUpdated,
                RespectPoints = _state.RespectPoints,
                RespectsLeft = _state.RespectsLeft,
                PetRespectsLeft = _state.PetRespectsLeft,
                RespectReplenishesLeft = _state.RespectReplenishesLeft,
            }
        );
    }

    public async Task<bool> TryUseRespectAsync(CancellationToken ct)
    {
        ResetDailyRespectIfDue();

        if (_state.RespectsLeft <= 0)
            return false;

        _state.RespectsLeft--;

        await WriteToDatabaseAsync(ct);

        return true;
    }

    public async Task<bool> TryUsePetRespectAsync(CancellationToken ct)
    {
        ResetDailyRespectIfDue();

        if (_state.PetRespectsLeft <= 0)
            return false;

        _state.PetRespectsLeft--;

        await WriteToDatabaseAsync(ct);

        return true;
    }

    public async Task<int> ReceiveRespectAsync(CancellationToken ct)
    {
        _state.RespectPoints++;

        await WriteToDatabaseAsync(ct);

        return _state.RespectPoints;
    }

    public async Task<bool> ReplenishRespectAsync(CancellationToken ct)
    {
        ResetDailyRespectIfDue();

        if (_state.RespectReplenishesLeft <= 0)
            return false;

        _state.RespectReplenishesLeft--;
        _state.RespectsLeft = _playerConfig.MaxRespectPerDay;

        await WriteToDatabaseAsync(ct);

        return true;
    }

    /// <summary>
    /// Daily respect allowances refill at UTC midnight. Checked lazily on every use, so a grain
    /// that stays active across midnight still resets.
    /// </summary>
    private void ResetDailyRespectIfDue()
    {
        var today = DateTime.UtcNow.Date;

        if (_state.RespectResetDate == today)
            return;

        _state.RespectResetDate = today;
        _state.RespectsLeft = _playerConfig.MaxRespectPerDay;
        _state.PetRespectsLeft = _playerConfig.MaxPetRespectPerDay;
        _state.RespectReplenishesLeft = _playerConfig.RespectReplenishesPerDay;
    }

    public async Task SetAchievementTotalsAsync(int score, int earnedLevels, CancellationToken ct)
    {
        _state.AchievementScore = score;
        _state.AchievementLevel = earnedLevels;
        _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .OnPlayerUpdatedAsync(await GetSummaryAsync(ct), CancellationToken.None)
            .LogAndForget(_logger, "publish achievement totals for player {PlayerId}", PlayerId);
    }

    public async Task SetBadgesRankAsync(int badgesRank, CancellationToken ct)
    {
        if (_state.BadgesRank == badgesRank)
            return;

        _state.BadgesRank = badgesRank;

        // Only the room shows a rank, so friends are not told as they are of a new figure.
        // Told, not awaited: the presence grain reads this grain, so awaiting it from here is the
        // other half of a deadlock.
        var summary = await GetSummaryAsync(ct);

        _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .OnBadgesRankChangedAsync(summary, CancellationToken.None)
            .LogAndForget(
                _logger,
                "tell player {PlayerId} presence of a new badges rank",
                PlayerId
            );
    }

    // The badge figures of a profile are not here: they are the badge grain's, and this grain
    // must not await it (badge grain -> presence -> this grain is already a chain). The handler
    // reads both and sends them side by side.
    /// <summary>
    /// Re-reads whether the player has a live session. Runs a turn after activation rather than
    /// during it; see the call site for why.
    /// </summary>
    private async Task RefreshOnlineStatusAsync() =>
        _state.IsOnline = await _grainFactory
            .GetPlayerPresenceGrain(_state.PlayerId)
            .HasActiveSessionAsync(CancellationToken.None);

    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            new PlayerExtendedProfileSnapshot
            {
                UserId = _state.PlayerId,
                UserName = _state.Name,
                Figure = PlayerLook
                    .Resolve(_state.Figure, _state.Gender, _state.LookOverride)
                    .Figure,
                Motto = _state.Motto,
                CreationDate = ClientDates.Format(_state.CreatedAt),
                AchievementScore = _state.AchievementScore,
                // The friends list is the messenger's, and the messenger awaits this grain,
                // so the profile's readers fill these in (PlayerService).
                FriendCount = 0,
                IsFriend = false,
                IsFriendRequestSent = false,
                IsOnline = _state.IsOnline,
                Guilds = [],
                // ExtendedProfileWindowCtrl shows "-" for -1: a player who never logged in.
                LastAccessSinceInSeconds = _state.LastLoginAtUtc is { } lastLogin
                    ? (int)Math.Max(0, (DateTime.UtcNow - lastLogin).TotalSeconds)
                    : -1,
                OpenProfileWindow = true,
                IsHidden = false,
                AccountLevel = 1,
                IntegerField24 = 0,
                StarGemCount = 0,
                BooleanField26 = false,
                BooleanField27 = false,
                AchievementLevel = _state.AchievementLevel,
            }
        );
}
