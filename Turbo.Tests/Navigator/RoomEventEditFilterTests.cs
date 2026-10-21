using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Advertisement;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// Editing a running event (the event window saves on losing focus, RoomEventViewCtrl.save). A
/// name or description holding a filtered word is answered with RoomAdError: code 0 marks the
/// name field, 1 the description, and the window puts the filtered text in that field
/// (RoomEventViewCtrl.onRoomAdError, roomad.error.0.description "Forbidden words filtered!").
/// Turbo saved the filtered text without a word to the player.
/// </summary>
public sealed class RoomEventEditFilterTests : IDisposable
{
    private const int OWNER = 1;
    private const int EVENT = 7;

    private readonly LiveRoomHarness _room = new();
    private readonly SqliteDb _db = new();

    public RoomEventEditFilterTests()
    {
        _room.Room.UseWordFilter(new BadIsUncleanFilter());
        _room.Room.UseDatabase(_db);

        var snapshot = _room.State.RoomSnapshot;
        _room.State.RoomSnapshot = snapshot with
        {
            ActiveEvent = new RoomEventSnapshot
            {
                EventId = EVENT,
                RoomId = snapshot.RoomId,
                OwnerId = OWNER,
                OwnerName = "test",
                CategoryId = 2,
                Name = "party",
                Description = "come along",
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            },
        };
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("bad party", "come along", 0, "bobba party")]
    [InlineData("party", "a bad time", 1, "a bobba time")]
    [InlineData("bad party", "a bad time", 0, "bobba party")]
    public async Task A_filtered_word_is_answered_with_its_field_and_filtered_text_and_not_saved(
        string name,
        string description,
        int code,
        string filtered
    )
    {
        var saved = await EditAsync(name, description);

        saved.Should().BeFalse();
        var error = SentToOwner()
            .OfType<RoomAdErrorEventMessageComposer>()
            .Should()
            .ContainSingle();
        error.Subject.ErrorCode.Should().Be(code);
        error.Subject.FilteredText.Should().Be(filtered);
        RoomEventSends().Should().BeEmpty();
    }

    [Fact]
    public async Task A_clean_edit_is_saved_and_announced_without_an_error()
    {
        var saved = await EditAsync("pool party", "bring towels");

        saved.Should().BeTrue();
        SentToOwner().OfType<RoomAdErrorEventMessageComposer>().Should().BeEmpty();
        RoomEventSends().Single().Event!.Name.Should().Be("pool party");
    }

    [Fact]
    public void The_error_is_written_as_the_client_reads_it_code_then_text()
    {
        var packet = PacketHarness.Encode(
            new RoomAdErrorEventMessageComposer { ErrorCode = 1, FilteredText = "a bobba time" }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("RoomAdErrorMessageComposer"));
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("a bobba time");
        packet.Remaining.Should().Be(0);
    }

    private Task<bool> EditAsync(string name, string description) =>
        _room.Room.UpdateEventAsync(
            ActionContext.CreateForPlayer(OWNER, _room.Room.RoomId),
            EVENT,
            name,
            description,
            CancellationToken.None
        );

    private IEnumerable<IComposer> SentToOwner() => _room.Fakes.SentToPlayer<IComposer>(OWNER);

    private List<RoomEventMessageComposer> RoomEventSends() =>
        [.. _room.Fakes.SentToRoom<RoomEventMessageComposer>()];

    private sealed class BadIsUncleanFilter : IWordFilter
    {
        public string Replacement => "bobba";

        public string Filter(string text) => text.Replace("bad", Replacement);

        public string Filter(string text, IReadOnlySet<string> extraWords) => Filter(text);

        public bool IsClean(string text) => !text.Contains("bad");

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
