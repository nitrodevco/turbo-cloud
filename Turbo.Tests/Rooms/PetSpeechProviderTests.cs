using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Migrations;
using Turbo.Primitives.Pets.Providers;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What a pet says comes from <c>pet_speech</c>, by its type: a type's own lines, else the lines
/// every type shares, else nothing. The shipped rows give each animal its own sounds.
/// </summary>
public sealed class PetSpeechProviderTests : IDisposable
{
    private const int DOG = 0;
    private const int CAT = 1;
    private const int COW = 35;

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task APetType_SaysItsOwnLines()
    {
        Add(DOG, "Woof!");
        Add(CAT, "Meow!");
        Add(CAT, "Purrr...");

        var speech = await LoadAsync();

        speech.GetLines(CAT).Should().Equal("Meow!", "Purrr...");
        speech.GetLines(DOG).Should().Equal("Woof!");
    }

    [Fact]
    public async Task ATypeWithNoLines_SaysTheSharedOnes_OrNothing()
    {
        Add(CAT, "Meow!");

        (await LoadAsync()).GetLines(COW).Should().BeEmpty("nothing is shared yet");

        Add(null, "...");

        (await LoadAsync()).GetLines(COW).Should().Equal("...");
        (await LoadAsync()).GetLines(CAT).Should().Equal(["Meow!"], "a type's own lines win");
    }

    [Fact]
    public void TheShippedLines_GiveEachAnimalItsOwnSounds()
    {
        var rows = new AddPetSpeech().UpOperations.OfType<InsertDataOperation>().Single().Values;
        var lines = Enumerable
            .Range(0, rows.GetLength(0))
            .Select(i => (Type: (int)rows[i, 0]!, Line: (string)rows[i, 1]!))
            .ToList();

        lines
            .Where(x => x.Line == "Woof!")
            .Select(x => x.Type)
            .Should()
            .OnlyContain(type => type == DOG || type == 36, "only the dog and the dragon dog bark");
        lines.Where(x => x.Type == CAT).Select(x => x.Line).Should().Contain("Meow!");
        lines.Should().OnlyContain(x => x.Line.Length > 0 && x.Line.Length <= 100);
    }

    private int _nextId;

    private void Add(int? typeId, string line) =>
        _db.Insert(
            new PetSpeechEntity
            {
                Id = ++_nextId,
                TypeId = typeId,
                Line = line,
            }
        );

    private async Task<IPetSpeechProvider> LoadAsync()
    {
        var provider = new PetSpeechProvider(_db, NullLogger<IPetSpeechProvider>.Instance);

        await provider.ReloadAsync(Ct);

        return provider;
    }
}
