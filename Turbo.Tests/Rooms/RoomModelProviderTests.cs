using FluentAssertions;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Exceptions;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room models are read once at startup. One that does not read must not take the server
/// down with it: installed from nothing on 2026-10-29, a disabled public room model with a
/// stray control character in a row stopped the host before it listened.
/// </summary>
public sealed class RoomModelProviderTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly CapturingLogger<IRoomModelProvider> _logger = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_model_that_does_not_read_is_left_out_and_the_others_load()
    {
        _db.Insert(Model(1, "model_a", "xxxx\rx000\rx000"));
        _db.Insert(Model(2, "the_den", "xxxx\rx0\u00020\rx000"));
        _db.Insert(Model(3, "model_b", "x0\r00"));

        var provider = new RoomModelProvider(_db, _logger);

        await provider.Invoking(x => x.ReloadAsync(Ct)).Should().NotThrowAsync();

        provider.GetModelById(1).Width.Should().Be(4);
        provider.GetModelById(3).Height.Should().Be(2);
        provider.Invoking(x => x.GetModelById(2)).Should().Throw<RoomModelNotFoundException>();

        _logger
            .AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle(x =>
                x.Message.Contains("the_den") && x.Message.Contains("has no height")
            );
        _logger
            .Entries.Should()
            .Contain(x => x.Level == LogLevel.Information && x.Message.Contains("LeftOut=1"));
    }

    [Fact]
    public async Task Reloading_one_model_still_says_when_it_does_not_read()
    {
        // A floor plan being saved is the room owner's input, and the save is told what is wrong.
        _db.Insert(Model(1, "custom", "x?\r00"));

        var provider = new RoomModelProvider(_db, _logger);

        await provider
            .Invoking(x => x.ReloadModelAsync(1, Ct))
            .Should()
            .ThrowAsync<RoomModelDataInvalidException>();
    }

    private static RoomModelEntity Model(int id, string name, string model) =>
        new()
        {
            Id = id,
            Name = name,
            Model = model,
            DoorX = 1,
            DoorY = 1,
            DoorRotation = Rotation.East,
            Enabled = true,
            Custom = false,
        };
}
