using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Pets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What a pet says comes from <c>pet_speech_lines</c>, by its type: a type's own lines, else the
/// lines every type shares, else nothing. The shipped rows are checked in
/// <see cref="Database.PostgresMigrationTests"/>.
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
