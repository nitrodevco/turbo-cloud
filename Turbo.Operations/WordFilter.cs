using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Moderation;

namespace Turbo.Operations;

/// <summary>
/// The hotel's word filter, read from <c>filter_words</c> at start and on <c>:reload filter</c>.
/// </summary>
public sealed class WordFilter(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<OperationsConfig> config,
    ILogger<IWordFilter> logger
) : IWordFilter
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly OperationsConfig _config = config.Value;
    private readonly ILogger<IWordFilter> _logger = logger;

    // Replaced whole on a reload, never cleared and refilled, so a reload that fails keeps the
    // words the hotel was already filtering (as ChatStyleProvider does).
    private FrozenSet<string> _words = FrozenSet<string>.Empty;

    public string Replacement => _config.WordFilterReplacement;

    public string Filter(string text) => FilterWords.Apply(text, _words, Replacement);

    public string Filter(string text, IReadOnlySet<string> extraWords) =>
        FilterWords.Apply(Filter(text), extraWords, Replacement);

    public bool IsClean(string text) => !FilterWords.ContainsAny(text, _words);

    public async Task ReloadAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var words = await dbCtx
                .FilterWords.AsNoTracking()
                .Select(x => x.Word)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            _words = words
                .Select(word => word.Trim())
                .Where(word => word.Length > 0)
                .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Loaded filter words: Count={Count}", _words.Count);
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
