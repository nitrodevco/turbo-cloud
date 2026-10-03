using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AchievementCatalog>? _logger;
    private readonly IAchievementPackRegistry? _packs;
    private readonly AchievementBadgeAssets _badgeAssets;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _publication = new(1, 1);
    private readonly Dictionary<(string, int), AchievementSourceDefinition> _sources = [];
    private ImmutableArray<AchievementDefinition> _current = [];
    public string DefaultCategory => _config.DefaultCategory;

    public AchievementCatalog(
        IDbContextFactory<TurboDbContext> database,
        ICurrencyTypeProvider currencies,
        IOptions<AchievementConfig> config,
        IHotelTextProvider texts,
        ILogger<AchievementCatalog>? logger = null,
        IAchievementPackRegistry? packs = null,
        AchievementBadgeAssets? badgeAssets = null
    )
    {
        _logger = logger;
        _packs = packs;
        _badgeAssets = badgeAssets ?? new AchievementBadgeAssets(config);
        _database = database;
        _currencies = currencies;
        _config = config.Value;
        _texts = texts;
        foreach (var source in CoreAchievementSources.All)
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
                    || (
                        !x.AlsoReducers.IsDefaultOrEmpty
                        && x.AlsoReducers.Any(r => !Enum.IsDefined(r))
                    )
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

    /// <summary>
    /// Installs every registered pack. A pack only adds: a definition whose key or id the hotel
    /// already has is left exactly as it is, so hotel edits and retirements survive a new pack
    /// version while its new achievements still arrive. Each pack goes through the same audited,
    /// validated import an administrator would run, and one failing (for example no badge asset
    /// directory or texts yet) never stops another or startup.
    /// </summary>
    private async Task InstallPacksAsync(CancellationToken ct)
    {
        if (_packs is null)
            return;
        foreach (var pack in _packs.Packs.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            try
            {
                await InstallPackAsync(pack, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "The achievement pack {Pack} was not installed. Fix what the error names (badge asset directory, badge texts), then run: achievement reload",
                    pack.Key
                );
            }
        }
    }

    private async Task InstallPackAsync(IAchievementPack pack, CancellationToken ct)
    {
        var db = await _database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var stored = (
            await db.AchievementDefinitions.AsNoTracking().ToListAsync(ct).ConfigureAwait(false)
        )
            .Select(x => AchievementDefinitionJson.Read(x.DefinitionJson))
            .ToList();
        var keys = stored.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = stored.Select(x => x.Id).ToHashSet();
        var missing = pack
            .Definitions.Where(x => !keys.Contains(x.Key) && !ids.Contains(x.Id))
            .ToImmutableArray();
        foreach (
            var clash in pack.Definitions.Where(x => !keys.Contains(x.Key) && ids.Contains(x.Id))
        )
            _logger?.LogWarning(
                "Pack {Pack} was not given achievement {Key}: id {Id} is already used by another achievement",
                pack.Key,
                clash.Key,
                clash.Id
            );
        if (missing.IsEmpty)
            return;
        // The same pack version can have different definitions still missing from one start to the
        // next, and an operation id must always mean the same request, so it names what it adds.
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',', missing.Select(x => x.Key))))
        )[..8];
        await ImportAsync(
                missing,
                apply: true,
                actor: "turbo",
                reason: $"Install the {pack.Key} achievement pack",
                operationId: $"pack:{pack.Key}:{pack.Version}:{fingerprint}",
                ct
            )
            .ConfigureAwait(false);
        _logger?.LogInformation(
            "Installed {Count} achievements from the {Pack} pack.",
            missing.Length,
            pack.Key
        );
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        await InstallPacksAsync(ct).ConfigureAwait(false);
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
                .Select(x => AchievementDefinitionJson.Read(x.DefinitionJson))
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
            var existing = rows.Select(x => AchievementDefinitionJson.Read(x.DefinitionJson))
                .ToArray();
            var combined = existing
                .GroupBy(x => x.Id)
                .Select(x => x.MaxBy(d => d.Revision)!)
                .Where(x => !definitions.Any(d => d.Id == x.Id))
                .Concat(definitions)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id)
                .ToImmutableArray();
            await _badgeAssets
                .CheckAsync(
                    combined
                        .Where(x => x.State == AchievementState.Enabled)
                        .SelectMany(x => x.Levels)
                        .Select(x => x.BadgeCode),
                    ct
                )
                .ConfigureAwait(false);
            Validate(combined, allowRetainedSources: false);
            foreach (var definition in definitions)
            {
                var history = existing.Where(x => x.Id == definition.Id).ToArray();
                if (
                    history.Any(x =>
                        x.Key != definition.Key
                        // Nothing records the placeholder source, so no progress can exist yet and
                        // the definition may be moved to a real source and reducer once.
                        || (
                            x.Source != AchievementSources.UNHOOKED
                            && (
                                x.Source != definition.Source
                                || x.SourceVersion != definition.SourceVersion
                                || x.Reducer != definition.Reducer
                            )
                        )
                        || x.Levels.Length > definition.Levels.Length
                    )
                )
                    throw new InvalidOperationException(
                        "An existing achievement identity, source, reducer or level count cannot be reinterpreted."
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
                    d.Id < 1
                    || (!allowRetainedSources && !IdAllowed(d.Id))
                    || !ValidKey(d.Key)
                    || !ValidKey(d.Category)
                    || d.Revision <= 0
                    || d.SourceVersion <= 0
                    || d.UnitDivisor <= 0
                    || d.Levels.IsDefaultOrEmpty
                    || d.Levels.Length > 100
                    || d.DisplayMethod is < 0 or > 2
                    || !Enum.IsDefined(d.State)
                    || d.State == AchievementState.WiredControlled
                )
                    throw new InvalidOperationException(
                        "Invalid achievement identity, units or packet limits."
                    );
                if (
                    d.ActiveFromUtc is { Kind: not DateTimeKind.Utc }
                    || d.ActiveUntilUtc is { Kind: not DateTimeKind.Utc }
                    || (
                        d.ActiveFromUtc is { } activeFrom
                        && d.ActiveUntilUtc is { } activeUntil
                        && activeFrom >= activeUntil
                    )
                )
                    throw new InvalidOperationException(
                        "An active window needs UTC times with the start before the end."
                    );
                if (!_sources.TryGetValue((d.Source, d.SourceVersion), out var source))
                {
                    if (!allowRetainedSources)
                        throw new InvalidOperationException("Unknown achievement source/version.");
                }
                else if (!source.Allows(d.Reducer))
                    throw new InvalidOperationException("Source reducer mismatch.");
                if (
                    !allowRetainedSources
                    && d.State == AchievementState.Enabled
                    && d.Source == AchievementSources.UNHOOKED
                )
                    throw new InvalidOperationException(
                        $"Achievement {d.Key} cannot be enabled: nothing records facts for it yet. Move it to a source something records first."
                    );
                if (
                    d.Match is { } narrowing
                    && (
                        !Enum.IsDefined(narrowing.ValueFrom)
                        || narrowing.Values.IsDefault
                        || narrowing.Values.Length > _config.MaxMatchValues
                        || narrowing.Values.Any(x =>
                            string.IsNullOrEmpty(x) || x.Length > MAX_FACT_VALUE_LENGTH
                        )
                        || (
                            narrowing.ValueFrom == AchievementValueSource.UtcDate
                            && (
                                d.Reducer != AchievementReducer.Distinct
                                || narrowing.Values.Length > 0
                            )
                        )
                    )
                )
                    throw new InvalidOperationException(
                        "Invalid match: a UTC-date value needs the distinct reducer and no value list, and values must be 1-512 characters."
                    );
                var previous = d.Reducer == AchievementReducer.Rank ? int.MaxValue : -1;
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
                        level.Requirement < 0
                        || (d.Reducer == AchievementReducer.Rank && level.Requirement == 0)
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
                    if (!allowRetainedSources && d.State == AchievementState.Enabled)
                    {
                        if (!_badgeAssets.Exists(level.BadgeCode))
                            throw new InvalidOperationException(
                                $"Enabled achievement {d.Key} requires the badge image {_badgeAssets.Describe(level.BadgeCode)}."
                            );
                        if (
                            !HasBadgeText("badge_name_", level.BadgeCode)
                            || !HasBadgeText("badge_desc_", level.BadgeCode)
                        )
                            throw new InvalidOperationException(
                                $"Enabled achievement {d.Key} requires a localized badge name and description for {level.BadgeCode}."
                            );
                    }
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

    /// <summary>The longest value a fact may carry (<c>AchievementFactRecorder</c>), so a longer match could never fire.</summary>
    private const int MAX_FACT_VALUE_LENGTH = 512;

    /// <summary>
    /// A hotel's own achievements start at <see cref="AchievementIds.CUSTOM_START"/>; below it an id
    /// belongs to a registered pack. Rows already stored are not checked, so a plugin's pack that
    /// is no longer installed never stops the hotel from starting.
    /// </summary>
    private bool IdAllowed(int id) =>
        id >= AchievementIds.CUSTOM_START
        || (_packs?.Packs.Any(p => p.IdRanges.Any(r => r.Contains(id))) ?? false);

    private static bool ValidKey(string key) => Regex.IsMatch(key, "^[a-z][a-z0-9_.-]{0,63}$");
}
