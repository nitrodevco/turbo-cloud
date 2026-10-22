using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Players.Configuration;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Nux;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Onboarding.Snapshots;

namespace Turbo.Players.Grains.Onboarding;

/// <summary>
/// A new player's gifts (<see cref="OnboardingConfig.Gifts"/>), write-through: a step picked is
/// saved, as a free product claim (<c>player_product_claims</c>, claim id <c>nux:day:step</c>,
/// choice the gift's index), before what the gift is made of is handed out, so a second answer
/// finds it taken.
/// </summary>
internal sealed class PlayerNuxGiftGrain : Grain, IPlayerNuxGiftGrain
{
    private const string CLAIM_PREFIX = "nux:";

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly OnboardingConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly IFurnitureDefinitionProvider _definitions;
    private readonly IPetBreedProvider _petBreeds;
    private readonly ILogger<IPlayerNuxGiftGrain> _logger;

    private readonly PlayerNuxGiftLiveState _state;

    public PlayerNuxGiftGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<OnboardingConfig> config,
        IGrainFactory grainFactory,
        IFurnitureDefinitionProvider definitions,
        IPetBreedProvider petBreeds,
        ILogger<IPlayerNuxGiftGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _definitions = definitions;
        _petBreeds = petBreeds;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        _state.SignedUpAt = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == _state.PlayerId.Value)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var taken = await dbCtx
            .PlayerProductClaims.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId == _state.PlayerId.Value && x.ClaimId.StartsWith(CLAIM_PREFIX)
            )
            .Select(x => x.ClaimId)
            .ToListAsync(ct);

        _state.Taken.UnionWith(taken);

        await base.OnActivateAsync(ct);
    }

    public Task<ImmutableArray<NuxGiftStepSnapshot>> GetDueStepsAsync(CancellationToken ct) =>
        Task.FromResult(
            DueSteps()
                .Select(step => new NuxGiftStepSnapshot
                {
                    DayIndex = step.Day,
                    StepIndex = step.Step,
                    Options =
                    [
                        .. step.Options.Select(gift => new NuxGiftOptionSnapshot
                        {
                            ThumbnailUrl = gift.Thumbnail,
                            Products =
                            [
                                .. gift.Products.Select(product => new NuxGiftProductSnapshot
                                {
                                    ProductCode = product.ProductCode,
                                    LocalizationKey = product.LocalizationKey,
                                }),
                            ],
                        }),
                    ],
                })
                .ToImmutableArray()
        );

    public async Task OfferAsync(CancellationToken ct)
    {
        var steps = await GetDueStepsAsync(ct);

        if (steps.IsEmpty)
            return;

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new NewUserExperienceGiftOfferEventMessageComposer { Steps = steps },
            ct
        );
    }

    public async Task<int> SelectAsync(
        ImmutableArray<NuxGiftSelectionSnapshot> selections,
        CancellationToken ct
    )
    {
        var taken = 0;

        foreach (var selection in selections)
        {
            var step = DueSteps()
                .FirstOrDefault(x => x.Day == selection.DayIndex && x.Step == selection.StepIndex);

            if (
                step is null
                || selection.GiftIndex < 0
                || selection.GiftIndex >= step.Options.Count
            )
            {
                _logger.LogWarning(
                    "Player {PlayerId} picked gift {GiftIndex} of new user gift step {Day}:{Step}, which is not offered to them",
                    _state.PlayerId,
                    selection.GiftIndex,
                    selection.DayIndex,
                    selection.StepIndex
                );

                continue;
            }

            if (await TakeAsync(step, selection.GiftIndex, ct))
                taken++;
        }

        return taken;
    }

    /// <summary>
    /// The steps offered now: from their day on, while the player is within
    /// <see cref="OnboardingConfig.GiftDays"/> of sign-up, not picked from yet and with a gift.
    /// </summary>
    private ImmutableArray<NuxGiftStep> DueSteps()
    {
        if (_state.SignedUpAt is not { } signedUpAt || _config.Gifts.Count == 0)
            return [];

        var day = (int)Math.Floor((DateTime.UtcNow - signedUpAt).TotalDays) + 1;

        if (_config.GiftDays > 0 && day > _config.GiftDays)
            return [];

        return _config
            .Gifts.Where(step =>
                step.Day <= day && step.Options.Count > 0 && !_state.Taken.Contains(ClaimIdOf(step))
            )
            .GroupBy(step => (step.Day, step.Step))
            .Select(same => same.First())
            .OrderBy(step => step.Day)
            .ThenBy(step => step.Step)
            .ToImmutableArray();
    }

    private async Task<bool> TakeAsync(NuxGiftStep step, int giftIndex, CancellationToken ct)
    {
        var claimId = ClaimIdOf(step);
        var gift = step.Options[giftIndex];

        // Checked before the step is written: a gift naming something the hotel cannot give
        // leaves the step open, rather than spending it on nothing.
        var given = gift.Products.Select(product => (product, Given: Resolve(product))).ToList();

        if (given.Any(x => x.Given is null))
        {
            _logger.LogError(
                "New user gift step {ClaimId} gift {GiftIndex} names a furni or pet the hotel does not have: {Products}",
                claimId,
                giftIndex,
                string.Join(
                    ", ",
                    given.Where(x => x.Given is null).Select(x => x.product.ProductCode)
                )
            );

            return false;
        }

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.PlayerProductClaims.Add(
                new PlayerProductClaimEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    ClaimId = claimId,
                    Choice = giftIndex,
                }
            );

            await dbCtx.SaveChangesAsync(ct);
        }

        _state.Taken.Add(claimId);

        var inventory = _grainFactory.GetInventoryGrain(_state.PlayerId);
        var cause = MovementCause.New(
            MovementCauses.ONBOARDING_GIFT,
            _state.PlayerId.Value,
            claimId
        );

        foreach (var (product, resolved) in given)
        {
            for (var i = 0; i < Math.Max(1, product.Amount); i++)
            {
                if (resolved!.Value.Pet is { } pet)
                {
                    await inventory.CreatePetAsync(
                        product.PetName.Length > 0
                            ? product.PetName
                            : NuxGiftProduct.DefaultPetName,
                        pet.Palette.TypeId,
                        pet.Palette.PaletteId,
                        pet.Palette.BreedId,
                        pet.Color,
                        pet.Palette.RarityLevel,
                        [],
                        cause,
                        ct
                    );
                }
                else
                {
                    await inventory.GrantFurnitureAsync(resolved.Value.FurniId, null, cause, ct);
                }
            }
        }

        _logger.LogInformation(
            "Player {PlayerId} picked gift {GiftIndex} of new user gift step {ClaimId}",
            _state.PlayerId,
            giftIndex,
            claimId
        );

        return true;
    }

    private static string ClaimIdOf(NuxGiftStep step) =>
        string.Create(CultureInfo.InvariantCulture, $"{CLAIM_PREFIX}{step.Day}:{step.Step}");

    /// <summary>The furni definition or the pet a product gives, or null when the hotel has neither.</summary>
    private (int FurniId, (PetBreedSnapshot Palette, string Color)? Pet)? Resolve(
        NuxGiftProduct product
    )
    {
        if (product.Pet.Length > 0)
            return PetOf(product.Pet) is { } pet ? (0, pet) : null;

        var name = product.Furni.Length > 0 ? product.Furni : product.ProductCode;

        return _definitions.TryGetDefinitionByName(name) is { } definition
            ? (definition.Id, null)
            : null;
    }

    /// <summary>The hotel's palette and the colour of a "type palette colour" pet figure, or null.</summary>
    private (PetBreedSnapshot Palette, string Color)? PetOf(string figure)
    {
        var parts = figure.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (
            parts.Length < 3
            || !int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var type
            )
            || !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var palette
            )
            || _petBreeds.TryGetPalette(type, palette) is not { } breed
        )
            return null;

        return (breed, parts[2]);
    }
}
