using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Assets.Nitro;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Entities.Pets;
using Turbo.Gamedata.Assets.Publishing;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Pets.Providers;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// A publish makes its pets known first: the client's pet types (<c>renderer.petTypes</c>) take
/// in the pet bundles at the type ids Habbo gives them, keeping the hotel's own order, and each
/// palette a bundle can be drawn in gets its <c>pet_breeds</c> row.
/// </summary>
public sealed class PublishedPetsTests : IDisposable
{
    private readonly AssetPublishHotel _hotel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _hotel.Dispose();

    [Fact]
    public async Task A_publish_places_the_pet_bundles_in_the_client_types_and_adds_their_breeds()
    {
        await SetTypesAsync("""["dog","cat","croco"]""");
        // Habbo's: cat is its type 1, gnome its 3 and 4, frog its 2 (which the hotel has as croco).
        await _hotel.PutPetAsync("cat", Pet("cat"), "1", Ct);
        await _hotel.PutPetAsync("gnome", Pet("gnome"), "3,4", Ct);
        await _hotel.PutPetAsync("frog", Pet("frog"), "2", Ct);
        // The hotel's own, with no type of Habbo's.
        await _hotel.PutPetAsync("dragondog", Pet("dragondog"), null, Ct);
        await KeepBreedAsync(
            new PetBreedEntity
            {
                TypeId = 1,
                PaletteId = 3,
                BreedId = 9,
                RarityLevel = 4,
                Sellable = false,
            }
        );

        var target = await _hotel.AddFolderTargetAsync(Ct);
        var dry = await _hotel.PublishAsync(target.Id, Ct, dryRun: true);

        dry.Log.Should().NotContain(x => x.StartsWith("Pets:"));
        (await TypesAsync()).Should().Equal("dog", "cat", "croco");

        var job = await _hotel.PublishAsync(target.Id, Ct);

        job.Log.Should().Contain(x => x.StartsWith("Pets:") && x.Contains("1 types kept"));
        // The hotel's croco keeps type 2; frog and the hotel's own come after Habbo's places.
        (await TypesAsync())
            .Should()
            .Equal("dog", "cat", "croco", "gnome", "gnome", "dragondog", "frog");

        var breeds = await BreedsAsync();

        // The master palette and the one for the hair alone are left out; breed and tag as the bundle has them.
        breeds
            .Where(x => x.TypeId == 3)
            .Select(x => (x.PaletteId, x.BreedId, x.ColorTag))
            .Should()
            .Equal((1, 2, 5), (3, 3, -1));
        breeds.Select(x => x.TypeId).Distinct().Order().Should().Equal(1, 3, 4, 5, 6);
        breeds.Should().NotContain(x => x.TypeId == 2 || x.TypeId == 0);

        // A row the hotel had is kept as it set it.
        breeds
            .Single(x => x.TypeId == 1 && x.PaletteId == 3)
            .Should()
            .Match<PetBreedEntity>(x => x.BreedId == 9 && x.RarityLevel == 4 && !x.Sellable);
        _hotel.Fakes.Log.Of(nameof(IPetBreedProvider.ReloadAsync)).Should().ContainSingle();

        // Nothing new the next time: the types are not saved again, nor breeds added.
        var again = await _hotel.PublishAsync(target.Id, Ct);

        again.Log.Should().Contain(x => x.StartsWith("Pets:") && x.Contains("0 breeds added"));
        _hotel.Fakes.Log.Of(nameof(IGamedataVariableService.SaveAsync)).Should().ContainSingle();
    }

    [Fact]
    public void A_pet_type_list_is_grown_to_the_ids_a_bundle_has()
    {
        PublishedPets
            .Arrange(["dog"], [("cat", [2])], out var clashes)
            .Should()
            .Equal("dog", "", "cat");
        clashes.Should().BeEmpty();
    }

    /// <summary>A pet bundle whose palettes are a master, one for the body, one for the hair alone, and a plain one.</summary>
    private static byte[] Pet(string name) =>
        NitroBundle
            .Create(
                new Dictionary<string, byte[]>
                {
                    [$"{name}.json"] = Encoding.UTF8.GetBytes(
                        """
                        {"type":"pet","palettes":[
                          {"id":0,"source":"p0","master":true,"tags":["body"]},
                          {"id":1,"source":"p1","tags":["body","head"],"breed":2,"colorTag":5},
                          {"id":2,"source":"p2","tags":["hair"],"breed":1},
                          {"id":3,"source":"p3"}
                        ]}
                        """
                    ),
                }
            )
            .Write();

    private async Task SetTypesAsync(string json)
    {
        await using var dbCtx = await _hotel.Db.CreateDbContextAsync(Ct);

        dbCtx.GamedataVariables.Add(
            new GamedataVariableEntity { Key = PublishedPets.PET_TYPES_VARIABLE, Value = json }
        );
        await dbCtx.SaveChangesAsync(Ct);
    }

    private async Task<string[]> TypesAsync()
    {
        await using var dbCtx = await _hotel.Db.CreateDbContextAsync(Ct);
        var value = await dbCtx
            .GamedataVariables.Where(x => x.Key == PublishedPets.PET_TYPES_VARIABLE)
            .Select(x => x.Value)
            .SingleAsync(Ct);

        return JsonSerializer.Deserialize<string[]>(value)!;
    }

    private async Task KeepBreedAsync(PetBreedEntity breed)
    {
        await using var dbCtx = await _hotel.Db.CreateDbContextAsync(Ct);

        dbCtx.PetBreeds.Add(breed);
        await dbCtx.SaveChangesAsync(Ct);
    }

    private async Task<List<PetBreedEntity>> BreedsAsync()
    {
        await using var dbCtx = await _hotel.Db.CreateDbContextAsync(Ct);

        return await dbCtx
            .PetBreeds.AsNoTracking()
            .OrderBy(x => x.TypeId)
            .ThenBy(x => x.PaletteId)
            .ToListAsync(Ct);
    }
}
