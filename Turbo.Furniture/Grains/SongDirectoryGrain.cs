using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Sound;
using Turbo.Database.Extensions;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Sound;
using Turbo.Primitives.Sound.Grains;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Furniture.Grains;

/// <summary>
/// Every trax song in the hotel, one grain. The <c>songs</c> rows are read on activation and
/// kept in memory, because a client asks for songs whenever it shows a disk, a playlist or a
/// song disk page; staff changes are written through and then replace the copy here, so there
/// is nothing to flush on deactivation.
/// </summary>
internal sealed class SongDirectoryGrain : Grain, ISongDirectoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IStuffDataFactory _stuffDataFactory;
    private readonly ILogger<ISongDirectoryGrain> _logger;

    private readonly SongDirectoryLiveState _state = new();

    public SongDirectoryGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IStuffDataFactory stuffDataFactory,
        ILogger<ISongDirectoryGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _stuffDataFactory = stuffDataFactory;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var songs = await dbCtx.Songs.AsNoTracking().ToListAsync(ct);

            foreach (var song in songs)
                Remember(song.ToSnapshot());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the song directory");

            throw;
        }
    }

    public Task<ImmutableArray<SongSnapshot>> GetSongsAsync(
        ImmutableArray<int> songIds,
        CancellationToken ct
    ) =>
        Task.FromResult<ImmutableArray<SongSnapshot>>([
            .. songIds
                .Distinct()
                .Select(id => _state.SongsById.GetValueOrDefault(id))
                .OfType<SongSnapshot>(),
        ]);

    public Task<int?> GetSongIdByCodeAsync(string code, CancellationToken ct) =>
        Task.FromResult<int?>(
            _state.IdsByCode.TryGetValue(code.Trim(), out var songId) ? songId : null
        );

    public Task<ImmutableArray<SongSnapshot>> GetAllSongsAsync(CancellationToken ct) =>
        Task.FromResult<ImmutableArray<SongSnapshot>>([
            .. _state.SongsById.Values.OrderBy(x => x.Id),
        ]);

    public async Task<ImmutableDictionary<int, int>> CountDisksAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        // The song is in the disk's stuff data, inside its JSON extra data, so the rows of song
        // disk furni are read and their songs counted here rather than grouped in SQL.
        var extraData = await dbCtx
            .Furnitures.AsNoTracking()
            .Where(x => x.FurnitureDefinitionEntity!.FurniCategory == FurnitureCategory.TraxSong)
            .Select(x => x.ExtraData)
            .ToListAsync(ct);

        return extraData
            .Select(SongIdOf)
            .Where(songId => songId != SongDisks.NO_SONG)
            .GroupBy(songId => songId)
            .ToImmutableDictionary(x => x.Key, x => x.Count());
    }

    public async Task<SongEditResult> CreateSongAsync(SongDraft draft, CancellationToken ct)
    {
        if (Check(draft, songId: null) is { } refused)
            return refused;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = new SongEntity
        {
            Name = draft.Name.Trim(),
            Author = draft.Author.Trim(),
            Track = draft.Track.Trim(),
        };

        Apply(entity, draft);

        dbCtx.Songs.Add(entity);

        await dbCtx.SaveChangesAsync(ct);

        var song = entity.ToSnapshot();

        Remember(song);

        return SongEditResult.Done(song);
    }

    public async Task<SongEditResult> UpdateSongAsync(
        int songId,
        SongDraft draft,
        CancellationToken ct
    )
    {
        if (!_state.SongsById.ContainsKey(songId))
            return SongEditResult.Missing();

        if (Check(draft, songId) is { } refused)
            return refused;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await dbCtx.Songs.FirstOrDefaultAsync(x => x.Id == songId, ct);

        if (entity is null)
        {
            Forget(songId);

            return SongEditResult.Missing();
        }

        Apply(entity, draft);

        await dbCtx.SaveChangesAsync(ct);

        var song = entity.ToSnapshot();

        Forget(songId);
        Remember(song);

        return SongEditResult.Done(song);
    }

    public async Task<SongEditResult> DeleteSongAsync(int songId, CancellationToken ct)
    {
        if (!_state.SongsById.ContainsKey(songId))
            return SongEditResult.Missing();

        // A disk of a deleted song would play nothing and could not be told apart from a
        // broken one, so a song stays while any disk carries it.
        if ((await CountDisksAsync(ct)).TryGetValue(songId, out var disks))
            return SongEditResult.Refused(
                $"{disks} song {(disks == 1 ? "disk carries" : "disks carry")} this song; it can't be deleted."
            );

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx.Songs.Where(x => x.Id == songId).ExecuteDeleteAsync(ct);

        Forget(songId);

        _logger.LogInformation("Deleted song {SongId}", songId);

        return SongEditResult.Done(null);
    }

    /// <summary>
    /// Why a draft cannot be saved, or null when it can. A code must not begin with a digit: the
    /// catalog's song disk page reads a product parameter that starts with one as a song id.
    /// </summary>
    private SongEditResult? Check(SongDraft draft, int? songId)
    {
        var name = draft.Name.Trim();
        var author = draft.Author.Trim();
        var code = Code(draft);

        if (name.Length == 0)
            return SongEditResult.Refused("Give the song a name.");

        if (name.Length > SongEntity.NAME_MAX_LENGTH)
            return SongEditResult.Refused(
                $"A song name can be at most {SongEntity.NAME_MAX_LENGTH} characters."
            );

        if (author.Length > SongEntity.AUTHOR_MAX_LENGTH)
            return SongEditResult.Refused(
                $"An author can be at most {SongEntity.AUTHOR_MAX_LENGTH} characters."
            );

        if (draft.Track.Trim().Length == 0)
            return SongEditResult.Refused("Give the song its track.");

        if (draft.LengthSeconds <= 0)
            return SongEditResult.Refused("A song has to last at least a second.");

        if (code is null)
            return null;

        if (code.Length > SongEntity.CODE_MAX_LENGTH)
            return SongEditResult.Refused(
                $"A song code can be at most {SongEntity.CODE_MAX_LENGTH} characters."
            );

        if (char.IsAsciiDigit(code[0]))
            return SongEditResult.Refused(
                "A song code can't begin with a digit; the catalog would read it as a song id."
            );

        if (_state.IdsByCode.TryGetValue(code, out var holder) && holder != songId)
            return SongEditResult.Refused($"Song {holder} already has the code {code}.");

        return null;
    }

    private static void Apply(SongEntity entity, SongDraft draft)
    {
        entity.Name = draft.Name.Trim();
        entity.Author = draft.Author.Trim();
        entity.Track = draft.Track.Trim();
        entity.LengthSeconds = draft.LengthSeconds;
        entity.Code = Code(draft);
        entity.IsOfficial = draft.IsOfficial;
    }

    private static string? Code(SongDraft draft) =>
        string.IsNullOrWhiteSpace(draft.Code) ? null : draft.Code.Trim();

    private int SongIdOf(string? extraDataJson) =>
        SongDisks.SongIdOf(
            _stuffDataFactory.CreateStuffDataFromExtraData(
                StuffDataType.LegacyKey,
                new ExtraData(extraDataJson)
            )
        );

    private void Remember(SongSnapshot song)
    {
        _state.SongsById[song.Id] = song;

        if (song.Code.Length > 0)
            _state.IdsByCode[song.Code] = song.Id;
    }

    private void Forget(int songId)
    {
        if (!_state.SongsById.Remove(songId, out var song))
            return;

        if (song.Code.Length > 0)
            _state.IdsByCode.Remove(song.Code);
    }
}
