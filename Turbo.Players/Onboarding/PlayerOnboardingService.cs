using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Players.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Onboarding;

namespace Turbo.Players.Onboarding;

/// <summary>
/// <see cref="IPlayerOnboardingService"/>. The steps still to do are the player's
/// <c>onboarding_*_pending</c> columns; the name rules are the sign-up's (<see cref="PlayerNames"/>
/// and the word filter) answered with Habbo's result codes.
/// </summary>
public sealed class PlayerOnboardingService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    IWordFilter wordFilter,
    IOptions<OnboardingConfig> config,
    IPlayerNamePolicy namePolicy
) : IPlayerOnboardingService
{
    /// <summary>The words suggestions are made with when the hotel names none.</summary>
    private static readonly string[] DEFAULT_SUGGESTION_WORDS =
    [
        "Cool",
        "Star",
        "Pixel",
        "Sunny",
        "Purple",
        "Cyclone",
        "Bonsai",
        "Mushroom",
        "Mammoth",
        "Jellyfish",
        "Blizzard",
        "Boulder",
        "Comet",
        "Breeze",
        "Racer",
        "Smith",
    ];

    public async Task<ImmutableArray<short>> GetLoginActionsAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var options = config.Value;

        if (!options.Enabled)
            return [];

        var steps = await ReadPendingAsync(playerId, ct).ConfigureAwait(false);

        if (steps is null)
            return [];

        var actions = ImmutableArray.CreateBuilder<short>();

        if (options.ChooseName && steps.Value.Name)
            actions.Add(IPlayerOnboardingService.CHOOSE_NAME);

        if (options.PickRoom && steps.Value.Room)
            actions.Add(IPlayerOnboardingService.PICK_ROOM);

        return actions.ToImmutable();
    }

    public async Task<NameCheck> CheckNameAsync(
        PlayerId playerId,
        string name,
        CancellationToken ct
    )
    {
        name = name?.Trim() ?? string.Empty;

        if (Refusal(name) is { } refused)
            return new(refused, name, []);

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var key = PlayerNames.Key(name);
        var owner = await db
            .Players.AsNoTracking()
            .Where(x => x.NameKey == key)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // Their own name is theirs to keep.
        if (owner is not null && owner != playerId.Value)
            return new(
                NameCheckResult.Taken,
                name,
                await SuggestAsync(db, name, ct).ConfigureAwait(false)
            );

        var pending = await db
            .Players.AsNoTracking()
            .Where(x => x.Id == playerId.Value)
            .Select(x => x.OnboardingNamePending)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // As Habbo answers once the name is chosen: any free name is 6.
        return pending && config.Value.Enabled
            ? new(NameCheckResult.Available, name, [])
            : new(NameCheckResult.ChangeNotAllowed, name, []);
    }

    public async Task<NameCheck> ChooseNameAsync(
        PlayerId playerId,
        string name,
        CancellationToken ct
    )
    {
        var check = await CheckNameAsync(playerId, name, ct).ConfigureAwait(false);

        if (check.Result != NameCheckResult.Available)
            return check;

        var result = await grainFactory
            .GetPlayerGrain(playerId)
            .ChooseNameAsync(check.Name, ct)
            .ConfigureAwait(false);

        return check with
        {
            Result = result,
        };
    }

    public async Task<bool> IsRoomPendingAsync(PlayerId playerId, CancellationToken ct) =>
        config.Value.Enabled
        && (await ReadPendingAsync(playerId, ct).ConfigureAwait(false))?.Room == true;

    public async Task<bool> CompleteRoomAsync(PlayerId playerId, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        // Conditional, so two picks at once make one room.
        var updated = await db
            .Players.Where(x => x.Id == playerId.Value && x.OnboardingRoomPending)
            .ExecuteUpdateAsync(up => up.SetProperty(x => x.OnboardingRoomPending, false), ct)
            .ConfigureAwait(false);

        return updated == 1;
    }

    private async Task<(bool Name, bool Room)?> ReadPendingAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .Players.AsNoTracking()
            .Where(x => x.Id == playerId.Value)
            .Select(x => new { x.OnboardingNamePending, x.OnboardingRoomPending })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return row is null ? null : (row.OnboardingNamePending, row.OnboardingRoomPending);
    }

    /// <summary>What is wrong with the name as written, before looking anyone up.</summary>
    private NameCheckResult? Refusal(string name)
    {
        if (name.Length < PlayerNames.MIN_LENGTH)
            return NameCheckResult.TooShort;

        if (name.Length > PlayerNames.MAX_LENGTH)
            return NameCheckResult.TooLong;

        // Characters, a leading @, the hotel's blocked names and the word filter: Flash shows 4
        // as "not a very nice name".
        if (
            PlayerNames.Check(name) is not null
            || namePolicy.IsBlocked(name)
            || !wordFilter.IsClean(name)
        )
            return NameCheckResult.NotAllowed;

        return null;
    }

    /// <summary>
    /// Free names made of the taken one and a word, as Habbo offers them ("HutchCyclone",
    /// "PurpleBill").
    /// </summary>
    private async Task<ImmutableArray<string>> SuggestAsync(
        TurboDbContext db,
        string name,
        CancellationToken ct
    )
    {
        var options = config.Value;
        var words = options.SuggestionWords.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        if (words.Count == 0)
            words = [.. DEFAULT_SUGGESTION_WORDS];

        if (options.NameSuggestions <= 0)
            return [];

        var candidates = words
            .SelectMany(word => new[] { name + word, word + name })
            .Where(x => Refusal(x) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var keys = candidates.Select(PlayerNames.Key).ToList();
        var taken = await db
            .Players.AsNoTracking()
            .Where(x => keys.Contains(x.NameKey))
            .Select(x => x.NameKey)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var takenKeys = new HashSet<string>(taken, StringComparer.Ordinal);

        return
        [
            .. candidates
                .Where(x => !takenKeys.Contains(PlayerNames.Key(x)))
                .Take(options.NameSuggestions),
        ];
    }
}
