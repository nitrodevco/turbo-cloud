using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Exceptions;

namespace Turbo.Rooms.Providers;

public sealed class RoomModelProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IRoomModelProvider> logger
) : IRoomModelProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IRoomModelProvider> _logger = logger;
    private ImmutableDictionary<int, RoomModelSnapshot> _modelsById = ImmutableDictionary<
        int,
        RoomModelSnapshot
    >.Empty;

    public RoomModelSnapshot GetModelById(int modelId) =>
        _modelsById.TryGetValue(modelId, out var model)
            ? model
            : throw new RoomModelNotFoundException(modelId);

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .RoomModels.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var models = ImmutableDictionary.CreateBuilder<int, RoomModelSnapshot>();
            var leftOut = 0;

            // A model that does not read (a character the format has no height for) is left
            // out with its reason, as a broken asset bundle is, instead of stopping the server:
            // the rooms built on the others keep opening, and the hotel is told which to fix.
            foreach (var entity in entities)
            {
                try
                {
                    var snapshot = ToSnapshot(entity);

                    models[snapshot.Id] = snapshot;
                }
                catch (RoomModelDataInvalidException ex)
                {
                    leftOut++;
                    _logger.LogError(
                        "Room model {ModelName} ({ModelId}) was left out: {Reason}",
                        entity.Name,
                        entity.Id,
                        ex.Message
                    );
                }
            }

            _modelsById = models.ToImmutable();

            _logger.LogInformation(
                "Loaded room models: TotalModels={TotalModelCount}, LeftOut={LeftOutCount}",
                _modelsById.Count,
                leftOut
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<RoomModelSnapshot?> ReloadModelAsync(int modelId, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var entity = await dbCtx
            .RoomModels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == modelId, ct)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogWarning("Room model {ModelId} was asked for and is not there", modelId);

            return null;
        }

        var snapshot = ToSnapshot(entity);

        // The whole dictionary is swapped rather than mutated, so a reader mid-lookup keeps the
        // map it started with instead of seeing half of two.
        Volatile.Write(ref _modelsById, _modelsById.SetItem(snapshot.Id, snapshot));

        return snapshot;
    }

    private static RoomModelSnapshot ToSnapshot(RoomModelEntity entity)
    {
        var modelData = CleanModelString(entity.Model);

        return entity.ToSnapshot(modelData, CompileModelFromString(modelData));
    }

    internal static string CleanModelString(string model) =>
        model.Trim().ToLowerInvariant().Replace("\r\n", "\r").Replace("\n", "\r");

    internal static CompiledRoomModelSnapshot CompileModelFromString(string model)
    {
        var rows = SplitLines(model);

        if (rows.Count == 0)
            throw new RoomModelDataInvalidException("the model contains no rows");

        var height = rows.Count;
        var width = rows.Max(x => x.Length);
        var size = width * height;
        var heights = new Altitude[size];
        var flags = new RoomTileFlags[size];

        for (var y = 0; y < height; y++)
        {
            var row = rows[y];

            for (var x = 0; x < width; x++)
            {
                int idx = y * width + x;
                char ch = (x < row.Length) ? row[x] : 'x';

                if (ch.Equals('x'))
                {
                    heights[idx] = Altitude.Zero;
                    flags[idx] = RoomTileFlags.Disabled | RoomTileFlags.StackBlocked;
                }
                else
                {
                    var heightIndex = "abcdefghijklmnopqrstuvwxyz".IndexOf(ch);
                    var tileHeight =
                        heightIndex == -1
                            ? Altitude.FromValue(
                                char.IsAsciiDigit(ch)
                                    ? ch - '0'
                                    : throw new RoomModelDataInvalidException(
                                        $"tile ({x}, {y}) has no height the model format knows"
                                    )
                            )
                            : Altitude.FromValue(heightIndex + 10);

                    heights[idx] = tileHeight;
                    flags[idx] = RoomTileFlags.Open;
                }
            }
        }

        return new CompiledRoomModelSnapshot
        {
            Width = width,
            Height = height,
            Heights = heights,
            Flags = flags,
        };
    }

    private static List<string> SplitLines(string s)
    {
        var lines = new List<string>();

        using var sr = new StringReader(s);

        string? line;

        while ((line = sr.ReadLine()) is not null)
            lines.Add(line);

        return lines;
    }
}
