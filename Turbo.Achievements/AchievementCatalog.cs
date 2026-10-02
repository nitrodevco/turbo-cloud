using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Achievements.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Texts;

namespace Turbo.Achievements;

/// <summary>Validated immutable revisions. A failed reload never replaces the published catalog.</summary>
public sealed class AchievementCatalog : IAchievementCatalog
{
    private readonly IDbContextFactory<TurboDbContext> _database;
    private readonly ICurrencyTypeProvider _currencies;
    private readonly AchievementConfig _config;
    private readonly IHotelTextProvider _texts;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _publication = new(1, 1);
    private readonly Dictionary<(string, int), AchievementSourceDefinition> _sources = [];
    private ImmutableArray<AchievementDefinition> _current = [];
    public string DefaultCategory => _config.DefaultCategory;

    public AchievementCatalog(
        IDbContextFactory<TurboDbContext> database,
        ICurrencyTypeProvider currencies,
        IOptions<AchievementConfig> config,
        IHotelTextProvider texts
    )
    {
        _database = database;
        _currencies = currencies;
        _config = config.Value;
        _texts = texts;
        foreach (var source in AchievementDefaults.Sources)
            _sources.Add((source.Key, source.Version), source);
    }

    public ImmutableArray<AchievementDefinition> Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public IDisposable RegisterSources(IEnumerable<AchievementSourceDefinition> sources)
    {
        var batch = sources.ToArray();
        lock (_gate)
        {
            if (
                batch.Length == 0
                || batch.Select(x => (x.Key, x.Version)).Distinct().Count() != batch.Length
                || batch.Any(x =>
                    !ValidKey(x.Key)
                    || x.Version <= 0
                    || !Enum.IsDefined(x.Reducer)
                    || _sources.ContainsKey((x.Key, x.Version))
                )
            )
                throw new ArgumentException(
                    "Invalid or colliding achievement sources.",
                    nameof(sources)
                );
            foreach (var source in batch)
                _sources.Add((source.Key, source.Version), source);
        }
        return new AchievementRegistration(() =>
        {
            lock (_gate)
                foreach (var source in batch)
                    _sources.Remove((source.Key, source.Version));
        });
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        await _publication.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var db = await _database.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);
            var rows = await db
                .AchievementDefinitions.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var next = rows.GroupBy(x => x.AchievementId)
                .Select(x => x.MaxBy(r => r.Revision)!)
                .Select(x =>
                    JsonSerializer.Deserialize<AchievementDefinition>(x.DefinitionJson)
                    ?? throw new InvalidOperationException("Empty achievement definition.")
                )
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id)
                .ToImmutableArray();
            Validate(next, allowRetainedSources: true);
            lock (_gate)
                _current = next;
        }
        finally
        {
            _publication.Release();
        }
    }

    public async Task ImportAsync(
        ImmutableArray<AchievementDefinition> definitions,
        bool apply,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 160)
            throw new ArgumentException(
                "Operation id exceeds 160 characters.",
                nameof(operationId)
            );
        await _publication.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var db = await _database.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);
            var request = JsonSerializer.Serialize(definitions);
            var receipt = await db
                .AchievementAudit.SingleOrDefaultAsync(x => x.OperationId == operationId, ct)
                .ConfigureAwait(false);
            if (receipt is not null)
            {
                if (
                    receipt.Actor != actor
                    || receipt.Reason != reason
                    || receipt.RequestJson != request
                )
                    throw new InvalidOperationException("Audit operation id collision.");
                return;
            }
            var rows = await db
                .AchievementDefinitions.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var existing = rows.Select(x =>
                    JsonSerializer.Deserialize<AchievementDefinition>(x.DefinitionJson)!
                )
                .ToArray();
            var combined = existing
                .GroupBy(x => x.Id)
                .Select(x => x.MaxBy(d => d.Revision)!)
                .Where(x => !definitions.Any(d => d.Id == x.Id))
                .Concat(definitions)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id)
                .ToImmutableArray();
            Validate(combined, allowRetainedSources: false);
            foreach (var definition in definitions)
            {
                var history = existing.Where(x => x.Id == definition.Id).ToArray();
                if (
                    history.Any(x =>
                        x.Key != definition.Key
                        || x.Source != definition.Source
                        || x.SourceVersion != definition.SourceVersion
                        || x.Reducer != definition.Reducer
                        || x.UnitDivisor != definition.UnitDivisor
                        || x.Levels.Length > definition.Levels.Length
                    )
                )
                    throw new InvalidOperationException(
                        "An existing achievement identity, source, units or level count cannot be reinterpreted."
                    );
                var sameRevision = history.FirstOrDefault(x => x.Revision == definition.Revision);
                if (
                    sameRevision is not null
                    && JsonSerializer.Serialize(sameRevision)
                        != JsonSerializer.Serialize(definition)
                )
                    throw new InvalidOperationException(
                        "Published definition revisions are immutable."
                    );
                if (history.Length > 0 && definition.Revision < history.Max(x => x.Revision))
                    throw new InvalidOperationException("Cannot publish an older revision.");
            }
            if (!apply)
                return;
            foreach (var definition in definitions)
                if (!existing.Any(x => x.Id == definition.Id && x.Revision == definition.Revision))
                    db.AchievementDefinitions.Add(
                        new()
                        {
                            AchievementId = definition.Id,
                            Revision = definition.Revision,
                            DefinitionJson = JsonSerializer.Serialize(definition),
                        }
                    );
            db.AchievementAudit.Add(
                new()
                {
                    OperationId = operationId,
                    Actor = actor,
                    Reason = reason,
                    RequestJson = request,
                    OccurredAtUtc = DateTime.UtcNow,
                    BeforeJson = JsonSerializer.Serialize(existing),
                    AfterJson = JsonSerializer.Serialize(combined),
                }
            );
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            lock (_gate)
                _current = combined;
        }
        finally
        {
            _publication.Release();
        }
    }

    private void Validate(
        ImmutableArray<AchievementDefinition> definitions,
        bool allowRetainedSources
    )
    {
        if (
            definitions.IsDefault
            || definitions.Length > _config.MaxDefinitions
            || definitions.Select(x => x.Id).Distinct().Count() != definitions.Length
            || definitions.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != definitions.Length
        )
            throw new InvalidOperationException("Invalid definition batch or colliding ids/keys.");
        var badgeCodes = definitions.SelectMany(x => x.Levels).Select(x => x.BadgeCode).ToArray();
        if (badgeCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != badgeCodes.Length)
            throw new InvalidOperationException(
                "Achievement badge codes cannot collide across the catalog."
            );
        lock (_gate)
            foreach (var d in definitions)
            {
                if (
                    d.Id < 1001
                    || (d.Id > 1018 && d.Id < 100000)
                    || !ValidKey(d.Key)
                    || !ValidKey(d.Category)
                    || d.Revision <= 0
                    || d.SourceVersion <= 0
                    || d.UnitDivisor <= 0
                    || d.Levels.IsDefaultOrEmpty
                    || d.Levels.Length > 100
                    || d.DisplayMethod is < 0 or > 2
                )
                    throw new InvalidOperationException(
                        "Invalid achievement identity, units or packet limits."
                    );
                if (!_sources.TryGetValue((d.Source, d.SourceVersion), out var source))
                {
                    if (!allowRetainedSources)
                        throw new InvalidOperationException("Unknown achievement source/version.");
                }
                else if (source.Reducer != d.Reducer)
                    throw new InvalidOperationException("Source reducer mismatch.");
                var previous = d.Reducer == AchievementReducer.Rank ? int.MaxValue : 0;
                if (d.Levels.Sum(x => (long)x.Score) > int.MaxValue)
                    throw new InvalidOperationException("Achievement score exceeds packet limits.");
                var badgeLevel = 0;
                string? badgePrefix = null;
                foreach (var level in d.Levels)
                {
                    badgeLevel++;
                    var match = Regex.Match(level.BadgeCode, "^([A-Za-z0-9_]*?)([0-9]+)$");
                    if (
                        !match.Success
                        || !int.TryParse(match.Groups[2].Value, out var suffix)
                        || suffix != badgeLevel
                        || (badgePrefix is not null && badgePrefix != match.Groups[1].Value)
                    )
                        throw new InvalidOperationException(
                            "Standard clients require one stable badge prefix with the earned level as its numeric suffix."
                        );
                    badgePrefix = match.Groups[1].Value;
                    if (
                        level.Requirement <= 0
                        || level.Score < 0
                        || !Regex.IsMatch(level.BadgeCode, "^ACH_[A-Za-z0-9_]{1,60}$")
                        || (
                            d.Reducer == AchievementReducer.Rank
                                ? level.Requirement >= previous
                                : level.Requirement <= previous
                        )
                    )
                        throw new InvalidOperationException(
                            "Invalid badge, score or cumulative requirements."
                        );
                    previous = level.Requirement;
                    if (
                        !allowRetainedSources
                        && d.Enabled
                        && !d.Archived
                        && (
                            string.IsNullOrWhiteSpace(_config.BadgeAssetDirectory)
                            || !File.Exists(
                                Path.Combine(_config.BadgeAssetDirectory, level.BadgeCode + ".png")
                            )
                            || !HasBadgeText("badge_name_", level.BadgeCode)
                            || !HasBadgeText("badge_desc_", level.BadgeCode)
                        )
                    )
                        throw new InvalidOperationException(
                            $"Enabled achievement {d.Key} requires badge image and localized name/description for {level.BadgeCode}."
                        );
                    foreach (var reward in level.Rewards)
                        if (
                            !ValidKey(reward.Handler)
                            || reward.Version <= 0
                            || reward.Amount < 0
                            || reward.Payload.Length > 65535
                            || (
                                reward.Handler == "wallet"
                                && (
                                    reward.Version != 1
                                    || reward.Currency is not { } kind
                                    || reward.Amount <= 0
                                    || (
                                        !allowRetainedSources
                                        && !_currencies.TryGetCurrencyTypeId(kind, out _)
                                    )
                                )
                            )
                        )
                            throw new InvalidOperationException(
                                "Invalid reward payload or currency."
                            );
                }
            }
    }

    private bool HasBadgeText(string prefix, string code)
    {
        // The client resolves a badge's exact text first, then its base text with level tokens.
        var badgeBase = Regex.Replace(code, "[0-9]+$", "");
        return _texts.TryGetText(prefix + code, out _)
            || _texts.TryGetText(prefix + badgeBase, out _);
    }

    private static bool ValidKey(string key) => Regex.IsMatch(key, "^[a-z][a-z0-9_.-]{0,63}$");
}
