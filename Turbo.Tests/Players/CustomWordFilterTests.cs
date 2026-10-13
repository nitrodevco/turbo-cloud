using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Preferences;
using Turbo.Players;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Settings;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The toolbar's Word Filter window (<c>WordFilterSettingsView</c>) asks for the player's own word
/// list as it opens and adds and removes words, listing a change only once the server confirms it
/// (result 1 added, 3 removed). Habbo answered an empty list (hhus, 2026-10-13); Turbo had no
/// parser for any of the three messages, so the window stayed empty and nothing was kept.
/// </summary>
public sealed class CustomWordFilterTests : IDisposable
{
    private const int PLAYER = 1;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CustomWordFilterTests() =>
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "player",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

    public void Dispose() => _db.Dispose();

    private async Task<IPlayerSettingsGrain> SettingsAsync()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Settings.PlayerSettingsGrain",
            _fakes,
            _db,
            PLAYER
        );
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerSettingsGrain)grain;
    }

    /// <summary>The handlers against the player's real settings grain; returns what was sent.</summary>
    private async Task<List<IComposer>> SendAsync(IPlayerSettingsGrain settings, object message)
    {
        var fakes = new Fakes();
        var factory = fakes.Create<IGrainFactory>();

        fakes.Handlers["GetGrain"] = _ => settings;

        var ctx = new MessageContext(fakes.Create<ISessionContext>(), 1, PLAYER);

        switch (message)
        {
            case GetCustomFilterMessage get:
                await new GetCustomFilterMessageHandler(factory).HandleAsync(get, ctx, Ct);
                break;
            case AddToCustomFilterMessage add:
                await new AddToCustomFilterMessageHandler(factory).HandleAsync(add, ctx, Ct);
                break;
            case RemoveFromCustomFilterMessage remove:
                await new RemoveFromCustomFilterMessageHandler(factory).HandleAsync(
                    remove,
                    ctx,
                    Ct
                );
                break;
        }

        return [.. fakes.Log.Of("SendComposerAsync").Select(x => (IComposer)x.Args[0]!)];
    }

    [Fact]
    public async Task an_empty_filter_is_answered_as_habbo_answers_it()
    {
        var sent = await SendAsync(await SettingsAsync(), new GetCustomFilterMessage());

        var packet = PacketHarness.Encode(sent.Single());

        Assert.Equal(Convert.FromHexString("00000000"), packet.PopBytes(packet.Remaining));
    }

    [Fact]
    public async Task a_word_added_is_confirmed_listed_and_kept_for_the_next_login()
    {
        var sent = await SendAsync(
            await SettingsAsync(),
            new AddToCustomFilterMessage { Word = "spoiler" }
        );

        sent.Should()
            .Equal(new ModifyCustomFilterResultMessageComposer { Result = 1, Word = "spoiler" });

        var again = await SendAsync(await SettingsAsync(), new GetCustomFilterMessage());

        again
            .Single()
            .Should()
            .BeOfType<GetCustomFilterResultMessageComposer>()
            .Which.Words.Should()
            .Equal("spoiler");
    }

    [Fact]
    public async Task a_word_already_listed_or_empty_is_refused_with_no_answer()
    {
        var settings = await SettingsAsync();
        await settings.AddCustomFilterWordAsync("spoiler", Ct);

        (await SendAsync(settings, new AddToCustomFilterMessage { Word = "SPOILER" }))
            .Should()
            .BeEmpty();
        (await SendAsync(settings, new AddToCustomFilterMessage { Word = "  " }))
            .Should()
            .BeEmpty();
        (await settings.GetCustomFilterWordsAsync(Ct)).Should().Equal("spoiler");
    }

    [Fact]
    public async Task a_word_removed_is_confirmed_and_gone_for_the_next_login()
    {
        await (await SettingsAsync()).AddCustomFilterWordAsync("spoiler", Ct);

        var sent = await SendAsync(
            await SettingsAsync(),
            new RemoveFromCustomFilterMessage { Word = "spoiler" }
        );

        sent.Should()
            .Equal(new ModifyCustomFilterResultMessageComposer { Result = 3, Word = "spoiler" });
        (await (await SettingsAsync()).GetCustomFilterWordsAsync(Ct)).Should().BeEmpty();
    }
}
