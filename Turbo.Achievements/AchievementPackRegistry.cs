using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Turbo.Primitives.Achievements;

namespace Turbo.Achievements;

public sealed class AchievementPackRegistry : IAchievementPackRegistry
{
    private readonly object _gate = new();
    private ImmutableArray<IAchievementPack> _packs = [];

    /// <param name="packs">The packs registered in the service container, validated at startup.</param>
    public AchievementPackRegistry(IEnumerable<IAchievementPack> packs)
    {
        foreach (var pack in packs)
            Register(pack);
    }

    public ImmutableArray<IAchievementPack> Packs
    {
        get
        {
            lock (_gate)
                return _packs;
        }
    }

    public IDisposable Register(IAchievementPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        lock (_gate)
        {
            Validate(pack, _packs);
            _packs = _packs.Add(pack);
        }

        return new AchievementRegistration(() =>
        {
            lock (_gate)
                _packs = _packs.Remove(pack);
        });
    }

    private static void Validate(IAchievementPack pack, ImmutableArray<IAchievementPack> registered)
    {
        if (
            !Regex.IsMatch(pack.Key, "^[a-z][a-z0-9_.-]{0,63}$")
            || pack.Version <= 0
            || pack.IdRanges.IsDefaultOrEmpty
            || pack.Definitions.IsDefault
        )
            throw new ArgumentException(
                "An achievement pack needs a lowercase key, a positive version and an id range.",
                nameof(pack)
            );
        if (registered.Any(x => x.Key == pack.Key))
            throw new ArgumentException(
                $"An achievement pack named {pack.Key} is already registered.",
                nameof(pack)
            );
        foreach (var range in pack.IdRanges)
            if (
                range.First < AchievementIds.PACK_START
                || range.Last < range.First
                || range.Last >= AchievementIds.CUSTOM_START
                || registered.Any(x => x.IdRanges.Any(r => r.Overlaps(range)))
                || pack.IdRanges.Count(r => r.Overlaps(range)) > 1
            )
                throw new ArgumentException(
                    $"The id range {range.First}-{range.Last} of pack {pack.Key} is invalid or owned by another pack.",
                    nameof(pack)
                );
        if (
            pack.Definitions.Any(d => !pack.IdRanges.Any(r => r.Contains(d.Id)))
            || pack.Definitions.Select(d => d.Id).Distinct().Count() != pack.Definitions.Length
            || pack.Definitions.Select(d => d.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != pack.Definitions.Length
        )
            throw new ArgumentException(
                $"Pack {pack.Key} has a definition outside its id ranges or a repeated id or key.",
                nameof(pack)
            );
    }
}
