using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Furniture.Configuration;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Furniture;

public sealed class FurnitureDefinitionProvider(
    IOptions<FurnitureConfig> config,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IFurnitureDefinitionProvider> logger
) : IFurnitureDefinitionProvider
{
    private readonly FurnitureConfig _config = config.Value;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IFurnitureDefinitionProvider> _logger = logger;

    private ImmutableDictionary<int, FurnitureDefinitionSnapshot> _definitionsById =
        ImmutableDictionary<int, FurnitureDefinitionSnapshot>.Empty;

    private ImmutableDictionary<string, FurnitureDefinitionSnapshot> _definitionsByName =
        ImmutableDictionary<string, FurnitureDefinitionSnapshot>.Empty;

    /// <summary>The definition names sorted ignoring case, for a prefix search.</summary>
    private string[] _sortedNames = [];

    public FurnitureDefinitionSnapshot? TryGetDefinition(int id) =>
        _definitionsById.TryGetValue(id, out var definition) ? definition : null;

    public FurnitureDefinitionSnapshot? TryGetDefinitionByName(string name) =>
        _definitionsByName.TryGetValue(name, out var definition) ? definition : null;

    public IReadOnlyList<string> FindNames(string prefix, int limit)
    {
        var names = _sortedNames;
        var found = new List<string>(Math.Min(limit, 16));

        // The names are sorted ignoring case, so those that begin with the prefix sit together
        // from the first one not below it.
        var index = Array.BinarySearch(names, prefix, StringComparer.OrdinalIgnoreCase);

        for (var i = index < 0 ? ~index : index; i < names.Length && found.Count < limit; i++)
        {
            if (!names[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                break;

            found.Add(names[i]);
        }

        return found;
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .FurnitureDefinitions.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var defs = entities.Select(x => x.ToSnapshot(_config.MinimumZValue)).ToList();

            _definitionsById = defs.ToImmutableDictionary(p => p.Id);

            _definitionsByName = defs.ToImmutableDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase
            );
            _sortedNames = [.. _definitionsByName.Keys.Order(StringComparer.OrdinalIgnoreCase)];

            _logger.LogInformation(
                "Loaded {TotalDefCount} furniture definitions",
                _definitionsById.Count
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
