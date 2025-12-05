using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Factories;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Factories;

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
            : throw new KeyNotFoundException($"Room model not found: ModelId={modelId}");

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .RoomModels.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            _modelsById = entities
                .Select(x =>
                {
                    var modelData = CleanModelString(x.Model);
                    var compiledModel = CompileModelFromString(modelData);

                    return new RoomModelSnapshot
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Model = modelData,
                        DoorX = x.DoorX,
                        DoorY = x.DoorY,
                        DoorRotation = x.DoorRotation,
                        Width = compiledModel.Width,
                        Height = compiledModel.Height,
                        Size = compiledModel.Width * compiledModel.Height,
                        BaseHeights = compiledModel.Heights,
                        BaseFlags = compiledModel.Flags,
                    };
                })
                .ToImmutableDictionary(x => x.Id);

            _logger.LogInformation(
                "Loaded room models: TotalModels={TotalModelCount}",
                _modelsById.Count
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string CleanModelString(string model) =>
        model.Trim().ToLower().Replace("\r\n", "\r").Replace("\n", "\r");

    private static CompiledRoomModelSnapshot CompileModelFromString(string model)
    {
        var rows = SplitLines(model);

        if (rows.Count == 0)
            throw new InvalidDataException("Room model data is empty.");

        var height = rows.Count;
        var width = rows.Max(x => x.Length);
        var size = width * height;
        var heights = new double[size];
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
                    heights[idx] = 0.0;
                    flags[idx] =
                        RoomTileFlags.Disabled | RoomTileFlags.Closed | RoomTileFlags.StackBlocked;
                }
                else
                {
                    var heightIndex = "abcdefghijklmnopqrstuvwxyz".IndexOf(ch);
                    var tileHeight =
                        heightIndex == -1 ? int.Parse(ch.ToString()) : heightIndex + 10;

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
