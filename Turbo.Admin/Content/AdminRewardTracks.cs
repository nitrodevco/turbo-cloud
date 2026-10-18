using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.RewardTracks;

namespace Turbo.Admin.Content;

/// <summary>
/// The panel's reward tracks: changing them, taking Habbo's in from a capture of what its client
/// was sent at login, and reading them again. Every change is written to the command log as
/// <c>rewardtracks &lt;what&gt; &lt;track ids&gt;</c> from the <c>panel</c>, and the players online
/// are sent the tracks again with <c>reload</c> set, so nobody needs a restart or a new login.
/// </summary>
public sealed class AdminRewardTracks(
    IRewardTrackStore store,
    IFurnitureDefinitionProvider definitions,
    ISessionGateway sessions,
    IGrainFactory grainFactory,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    TimeProvider time,
    ILogger<AdminRewardTracks> logger
)
{
    public const string COMMAND = "rewardtracks";
    public const string SOURCE = "panel";

    private const string GENERIC_BOT_TYPE = "GENERIC";

    public IReadOnlyList<RewardTrackListItem> List()
    {
        var now = time.GetUtcNow().UtcDateTime;

        return
        [
            .. store.Tracks.Select(x => new RewardTrackListItem(
                x.Id,
                x.Theme,
                x.Enabled,
                x.StartsAt,
                x.EndsAt,
                x.IsLiveAt(now),
                x.Premium is not null,
                x.Tasks.Count,
                x.Tasks.Sum(t => t.Levels.Count),
                x.Prizes.Count,
                x.Prizes.Count == 0 ? 0 : x.Prizes.Max(p => p.RequiredPoints)
            )),
        ];
    }

    public RewardTrackDefinition? Get(string trackId) =>
        store.Tracks.FirstOrDefault(x => x.Id == trackId);

    /// <summary>What the hotel cannot do of a track: tasks it does not count, prizes it cannot give.</summary>
    public string[] Warnings(RewardTrackDefinition track) =>
        [
            .. track
                .Tasks.Where(x =>
                    !RewardTrackActionTypes.ALL.Contains(
                        x.ActionType,
                        StringComparer.OrdinalIgnoreCase
                    )
                )
                .Select(x => $"task {x.Id}: the server does not count {x.ActionType}"),
            .. track
                .Tasks.Where(x =>
                    string.Equals(
                        x.ActionType,
                        RewardTrackActionTypes.CUSTOM_WIRED_ACTION,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .Select(x =>
                    $"task {x.Id}: counted only by a wired Progress Reward Track box naming {track.Id}/{x.Id}"
                ),
            .. track
                .Prizes.Select(x => (x.Id, Problem: GiveProblem(x)))
                .Where(x => x.Problem is not null)
                .Select(x => $"prize {x.Id}: {x.Problem}"),
        ];

    /// <summary>
    /// The furni each furni prize gives here (its definition, else the hotel's furni of its
    /// sprite), so the panel can show what the prize is.
    /// </summary>
    public RewardTrackPrizeFurni[] PrizeFurni(RewardTrackDefinition track) =>
        [
            .. track
                .Prizes.Select(x => (x.Id, Definition: FurniOf(x)))
                .Where(x => x.Definition is not null)
                .Select(x => new RewardTrackPrizeFurni(x.Id, x.Definition!.Id, x.Definition.Name)),
        ];

    public async Task<RewardTrackDefinition> SaveAsync(
        PlayerId staff,
        RewardTrackDefinition track,
        CancellationToken ct
    )
    {
        await store.SaveAsync([track], ct).ConfigureAwait(false);
        await ChangedAsync(staff, "save", [track.Id], ct).ConfigureAwait(false);

        return Get(track.Id)!;
    }

    public async Task<bool> DeleteAsync(PlayerId staff, string trackId, CancellationToken ct)
    {
        if (!await store.DeleteAsync(trackId, ct).ConfigureAwait(false))
            return false;

        await ChangedAsync(staff, "delete", [trackId], ct).ConfigureAwait(false);

        return true;
    }

    public async Task ReloadAsync(PlayerId staff, CancellationToken ct)
    {
        await store.ReloadAsync(ct).ConfigureAwait(false);
        await ChangedAsync(staff, "reload", [], ct).ConfigureAwait(false);
    }

    /// <summary>What taking the capture in would do; nothing is changed.</summary>
    public RewardTrackImportPlan Plan(RewardTrackImportRequest request) =>
        new([.. Merge(request).Select(x => x.Change)], Saved: false);

    /// <summary>Takes the capture in: every added or changed track is saved, in one transaction.</summary>
    public async Task<RewardTrackImportPlan> ImportAsync(
        PlayerId staff,
        RewardTrackImportRequest request,
        CancellationToken ct
    )
    {
        var merged = Merge(request);
        var changed = merged
            .Where(x => x.Change.Action != RewardTrackImportChange.UNCHANGED)
            .ToList();

        if (changed.Count > 0)
        {
            await store.SaveAsync([.. changed.Select(x => x.Track)], ct).ConfigureAwait(false);
            await ChangedAsync(staff, "import", [.. changed.Select(x => x.Track.Id)], ct)
                .ConfigureAwait(false);
        }

        return new([.. merged.Select(x => x.Change)], Saved: changed.Count > 0);
    }

    private List<(RewardTrackDefinition Track, RewardTrackImportChange Change)> Merge(
        RewardTrackImportRequest request
    )
    {
        var captured =
            request.Capture?.Tracks
            ?? throw new ArgumentException("The capture has no RewardTracks tracks.");
        var only = request.Only is { Length: > 0 } ids ? ids.ToHashSet() : null;
        var result = new List<(RewardTrackDefinition, RewardTrackImportChange)>();

        foreach (var capture in captured)
        {
            var track = Read(capture);

            if (only is not null && !only.Contains(track.Id))
                continue;

            var before = Get(track.Id);

            if (before is not null)
            {
                // The hotel's own choices stay: when players get it, and what a kept prize gives.
                track.Enabled = before.Enabled;
                track.StartsAt = before.StartsAt;
                track.EndsAt = before.EndsAt;

                foreach (var prize in track.Prizes)
                    if (before.Prizes.FirstOrDefault(x => x.Id == prize.Id) is { } kept)
                    {
                        prize.FurnitureDefinitionId = kept.FurnitureDefinitionId;
                        prize.Name = kept.Name;
                    }
            }

            result.Add((track, Change(before, track)));
        }

        if (RewardTrackRules.Problem([.. result.Select(x => x.Item1)]) is { } problem)
            throw new ArgumentException($"The capture is not usable: {problem}");

        return result;
    }

    private RewardTrackImportChange Change(
        RewardTrackDefinition? before,
        RewardTrackDefinition after
    )
    {
        var (tasksAdded, tasksChanged, tasksRemoved) = Diff(
            before?.Tasks ?? [],
            after.Tasks,
            x => x.Id
        );
        var (prizesAdded, prizesChanged, prizesRemoved) = Diff(
            before?.Prizes ?? [],
            after.Prizes,
            x => x.Id
        );
        var premiumChanged = before is not null && Json(before.Premium) != Json(after.Premium);
        var themeChanged = before is not null && before.Theme != after.Theme;
        // Order is what the client shows; a reordered list is a change too.
        var reordered =
            before is not null
            && (
                !before.Tasks.Select(x => x.Id).SequenceEqual(after.Tasks.Select(x => x.Id))
                || !before.Prizes.Select(x => x.Id).SequenceEqual(after.Prizes.Select(x => x.Id))
            );
        var action =
            before is null ? RewardTrackImportChange.ADDED
            : premiumChanged
            || themeChanged
            || reordered
            || tasksAdded.Length + tasksChanged.Length + tasksRemoved.Length > 0
            || prizesAdded.Length + prizesChanged.Length + prizesRemoved.Length > 0
                ? RewardTrackImportChange.CHANGED
            : RewardTrackImportChange.UNCHANGED;

        return new(
            after.Id,
            action,
            premiumChanged,
            themeChanged,
            tasksAdded,
            tasksChanged,
            tasksRemoved,
            prizesAdded,
            prizesChanged,
            prizesRemoved,
            Warnings(after)
        );
    }

    private static (string[] Added, string[] Changed, string[] Removed) Diff<T>(
        IReadOnlyList<T> before,
        IReadOnlyList<T> after,
        Func<T, string> id
    )
    {
        var old = before.ToDictionary(id);
        var next = after.ToDictionary(id);

        return (
            [.. after.Select(id).Where(x => !old.ContainsKey(x))],
            [
                .. after
                    .Where(x => old.TryGetValue(id(x), out var was) && Json(was) != Json(x))
                    .Select(id),
            ],
            [.. before.Select(id).Where(x => !next.ContainsKey(x))]
        );
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    private static RewardTrackDefinition Read(RewardTrackCaptureTrack capture)
    {
        if (string.IsNullOrWhiteSpace(capture.Id))
            throw new ArgumentException("A captured track has no id.");

        return new()
        {
            Id = capture.Id,
            Theme = capture.Theme ?? "",
            Premium = capture.HasPremiumConfig
                ? new()
                {
                    TaskPointsBoost = capture.TaskPointsBoost,
                    InstantPoints = capture.InstantPoints,
                    CostCredits = capture.CostCredits,
                    CostDiamonds = capture.CostDiamonds,
                }
                : null,
            Tasks =
            [
                .. (capture.Tasks ?? []).Select(task => new RewardTrackTaskDefinition
                {
                    Id = task.Id ?? "",
                    ActionType = task.ActionType ?? "",
                    Parameter = task.Parameter ?? "",
                    Premium = task.Premium,
                    Levels =
                    [
                        .. (task.Levels ?? []).Select(level => new RewardTrackTaskLevelDefinition
                        {
                            RequiredCount = level.RequiredCount,
                            Points = level.PointsReward,
                            Premium = level.Premium,
                        }),
                    ],
                }),
            ],
            Prizes =
            [
                .. (capture.Prizes ?? []).Select(prize => new RewardTrackPrizeDefinition
                {
                    Id = prize.Id ?? "",
                    RequiredPoints = prize.RequiredPoints,
                    ProductType = (ProductDisplayType)prize.ProductItemTypeId,
                    RewardTypeId = prize.RewardTypeId ?? "",
                    ExtraParams = prize.ExtraParams ?? "",
                    Amount = prize.RewardAmount,
                    Premium = prize.Premium,
                }),
            ],
        };
    }

    /// <summary>Why the server cannot give a prize, as <c>PlayerRewardTrackGrain</c> gives them, or null.</summary>
    private FurnitureDefinitionSnapshot? FurniOf(RewardTrackPrizeDefinition prize) =>
        prize.ProductType switch
        {
            ProductDisplayType.FloorItem
            or ProductDisplayType.WallItem when prize.FurnitureDefinitionId is { } id =>
                definitions.TryGetDefinition(id),
            ProductDisplayType.FloorItem
            or ProductDisplayType.WallItem
                when int.TryParse(
                    prize.RewardTypeId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var sprite
                ) => definitions.TryGetDefinitionBySprite(
                prize.ProductType == ProductDisplayType.WallItem
                    ? ProductType.Wall
                    : ProductType.Floor,
                sprite
            ),
            _ => null,
        };

    private string? GiveProblem(RewardTrackPrizeDefinition prize)
    {
        var number = int.TryParse(
            prize.RewardTypeId,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var id
        );

        return prize.ProductType switch
        {
            ProductDisplayType.ActivityPoints or ProductDisplayType.Habbicon when !number =>
                $"{prize.RewardTypeId} is not a number",
            ProductDisplayType.ActivityPoints or ProductDisplayType.Habbicon => null,
            ProductDisplayType.Badge when prize.RewardTypeId.Length == 0 => "no badge code",
            ProductDisplayType.Badge => null,
            ProductDisplayType.FloorItem
            or ProductDisplayType.WallItem when prize.FurnitureDefinitionId is not null => null,
            ProductDisplayType.FloorItem
            or ProductDisplayType.WallItem
                when !number
                    || definitions.TryGetDefinitionBySprite(
                        prize.ProductType == ProductDisplayType.WallItem
                            ? ProductType.Wall
                            : ProductType.Floor,
                        id
                    )
                        is null => $"no furni of sprite {prize.RewardTypeId} here",
            ProductDisplayType.FloorItem or ProductDisplayType.WallItem => null,
            ProductDisplayType.Pet when prize.ExtraParams.Length == 0 => "a pet with no figure",
            ProductDisplayType.Pet => null,
            ProductDisplayType.Bot
                when prize.ExtraParams.Length > 0
                    && (
                        prize.RewardTypeId.Length == 0
                        || string.Equals(
                            prize.RewardTypeId,
                            GENERIC_BOT_TYPE,
                            StringComparison.OrdinalIgnoreCase
                        )
                    ) => null,
            _ => $"product type {(short)prize.ProductType} cannot be given",
        };
    }

    private async Task ChangedAsync(
        PlayerId staff,
        string what,
        IReadOnlyList<string> trackIds,
        CancellationToken ct
    )
    {
        await AuditAsync(staff, string.Join(' ', [what, .. trackIds]), ct).ConfigureAwait(false);

        foreach (var playerId in sessions.GetOnlinePlayerIds())
            grainFactory
                .GetPlayerRewardTrackGrain(playerId)
                .ReloadTracksAsync(CancellationToken.None)
                .LogAndForget(logger, "send player {PlayerId} the changed reward tracks", playerId);
    }

    private async Task AuditAsync(PlayerId staff, string arguments, CancellationToken ct)
    {
        try
        {
            await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

            db.CommandLogs.Add(
                new CommandLogEntity
                {
                    RoomEntityId = 0,
                    PlayerEntityId = staff.Value,
                    Command = COMMAND,
                    Arguments =
                        arguments.Length <= CommandLogEntity.ARGUMENTS_MAX_LENGTH
                            ? arguments
                            : arguments[..CommandLogEntity.ARGUMENTS_MAX_LENGTH],
                    Outcome = CommandTelemetry.Name(CommandOutcome.Completed),
                    ExecutionId = Guid.NewGuid(),
                    Source = SOURCE,
                }
            );

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to write the command log entry for reward tracks {Arguments} by {Player}",
                arguments,
                staff
            );
        }
    }
}
