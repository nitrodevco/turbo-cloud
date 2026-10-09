using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Pets.Providers;

namespace Turbo.Rooms.Providers;

public sealed class PetSpeechProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IPetSpeechProvider> logger
) : IPetSpeechProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IPetSpeechProvider> _logger = logger;

    private ImmutableDictionary<int, ImmutableArray<string>> _linesByTypeId = ImmutableDictionary<
        int,
        ImmutableArray<string>
    >.Empty;

    private ImmutableArray<string> _sharedLines = [];

    public ImmutableArray<string> GetLines(int typeId) =>
        _linesByTypeId.TryGetValue(typeId, out var lines) ? lines : _sharedLines;

    public async Task ReloadAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .PetSpeech.AsNoTracking()
            .Where(x => x.Line != string.Empty)
            .OrderBy(x => x.Id)
            .Select(x => new { x.TypeId, x.Line })
            .ToListAsync(ct);

        _linesByTypeId = rows.Where(x => x.TypeId is not null)
            .GroupBy(x => x.TypeId!.Value)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.Select(x => x.Line).ToImmutableArray()
            );
        _sharedLines = [.. rows.Where(x => x.TypeId is null).Select(x => x.Line)];

        _logger.LogInformation(
            "Loaded {Count} pet speech lines for {Types} pet types and {Shared} shared",
            rows.Count,
            _linesByTypeId.Count,
            _sharedLines.Length
        );
    }
}
