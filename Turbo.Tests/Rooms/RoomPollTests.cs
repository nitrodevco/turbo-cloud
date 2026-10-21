using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Polls;
using Turbo.Database.Entities.Room;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Poll;
using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Polls;
using Turbo.Rooms.Grains.Polls;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Room polls, which Turbo never offered: the AS3 flow (PollWidget, PollOfferDialog,
/// PollContentDialog, build WIN63-202610131318) is an offer on entering the poll's room, Start for
/// the questions (PollContentsParser), one PollAnswer per question, Cancel or close to turn it
/// down and Later to put it off. No official capture of a room poll exists.
/// </summary>
public sealed class RoomPollTests : IDisposable
{
    private const int POLL_ID = 5;
    private const int ROOM_ID = 10;
    private const int SINGLE = 51;
    private const int TEXT = 52;
    private const int FOLLOW_UP = 53;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomPollTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "voter",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "poll-model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM_ID,
                Name = "poll-room",
                PlayerEntityId = 1,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
        _db.Insert(
            new RoomPollEntity
            {
                Id = POLL_ID,
                RoomEntityId = ROOM_ID,
                Type = "",
                Headline = "Tell us",
                Summary = "Two questions",
                StartMessage = "Here we go",
                EndMessage = "Thanks!",
                NpsPoll = true,
                Enabled = true,
            }
        );
        _db.Insert(Question(SINGLE, null, 1, RoomPollQuestionType.SingleChoice, "Like it?"));
        _db.Insert(Question(TEXT, null, 2, RoomPollQuestionType.TextLine, "Why?"));
        _db.Insert(Question(FOLLOW_UP, SINGLE, 1, RoomPollQuestionType.TextArea, "Not?"));
        _db.Insert(Choice(1, SINGLE, "1", "Yes", 0));
        _db.Insert(Choice(2, SINGLE, "2", "No", 1));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Entering_the_polls_room_offers_it_and_another_room_does_not()
    {
        await Grain().OfferAsync(new RoomId(ROOM_ID + 1), Ct);
        _fakes.Sent<PollOfferEventMessageComposer>().Should().BeEmpty();

        await Grain().OfferAsync(new RoomId(ROOM_ID), Ct);

        _fakes
            .Sent<PollOfferEventMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new PollOfferEventMessageComposer
                {
                    PollId = POLL_ID,
                    PollType = "",
                    Headline = "Tell us",
                    Summary = "Two questions",
                }
            );
    }

    [Fact]
    public async Task Start_sends_the_questions_with_follow_ups_under_their_question()
    {
        await Grain().StartAsync(new RoomId(ROOM_ID), POLL_ID, Ct);

        var contents = _fakes.Sent<PollContentsEventMessageComposer>().Single();

        contents.StartMessage.Should().Be("Here we go");
        contents.EndMessage.Should().Be("Thanks!");
        contents.NpsPoll.Should().BeTrue();
        contents.Questions.Select(x => x.QuestionId).Should().Equal(SINGLE, TEXT);
        contents.Questions[0].Choices.Select(x => x.Value).Should().Equal("1", "2");
        contents.Questions[0].Children.Single().QuestionId.Should().Be(FOLLOW_UP);
        contents.Questions[1].Children.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_from_another_room_is_a_poll_error()
    {
        await Grain().StartAsync(new RoomId(ROOM_ID + 1), POLL_ID, Ct);

        _fakes.Sent<PollErrorEventMessageComposer>().Should().ContainSingle();
        _fakes.Sent<PollContentsEventMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task Answers_are_kept_when_they_fit_the_question_and_the_poll_ends_after_the_last()
    {
        var grain = Grain();
        await grain.StartAsync(new RoomId(ROOM_ID), POLL_ID, Ct);

        (await grain.AnswerAsync(POLL_ID, SINGLE, ["9"], Ct)).Should().BeTrue("not a choice");
        (await grain.AnswerAsync(POLL_ID, SINGLE, ["2"], Ct)).Should().BeTrue();
        (await grain.AnswerAsync(POLL_ID, FOLLOW_UP, ["meh"], Ct)).Should().BeTrue();
        (await grain.AnswerAsync(POLL_ID, TEXT, [new string('x', 513)], Ct))
            .Should()
            .BeTrue("too long");

        Answers().Should().BeEquivalentTo([(SINGLE, "2"), (FOLLOW_UP, "meh")]);
        State().Should().Be(RoomPollState.Started);

        (await grain.AnswerAsync(POLL_ID, TEXT, ["because"], Ct)).Should().BeTrue();

        State().Should().Be(RoomPollState.Finished);
        await grain.OfferAsync(new RoomId(ROOM_ID), Ct);
        _fakes.Sent<PollOfferEventMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task An_answer_to_a_poll_not_started_is_not_the_polls()
    {
        (await Grain().AnswerAsync(POLL_ID, SINGLE, ["1"], Ct)).Should().BeFalse();

        Answers().Should().BeEmpty();
    }

    [Fact]
    public async Task A_poll_turned_down_is_not_offered_or_started_again()
    {
        var grain = Grain();

        await grain.RejectAsync(POLL_ID, Ct);
        await grain.OfferAsync(new RoomId(ROOM_ID), Ct);
        await grain.StartAsync(new RoomId(ROOM_ID), POLL_ID, Ct);

        State().Should().Be(RoomPollState.Rejected);
        _fakes.Sent<PollOfferEventMessageComposer>().Should().BeEmpty();
        _fakes.Sent<PollErrorEventMessageComposer>().Should().ContainSingle();
    }

    [Fact]
    public async Task A_poll_started_but_not_finished_is_offered_again()
    {
        var grain = Grain();

        await grain.StartAsync(new RoomId(ROOM_ID), POLL_ID, Ct);
        await grain.OfferAsync(new RoomId(ROOM_ID), Ct);

        _fakes.Sent<PollOfferEventMessageComposer>().Should().ContainSingle();
    }

    [Fact]
    public async Task A_poll_answer_goes_to_the_word_quiz_when_no_poll_takes_it()
    {
        _fakes.Handlers["AnswerAsync"] = _ => Task.FromResult(false);

        await AnswerHandler().HandleAsync(Answer(), Context(), Ct);

        _fakes.Log.Of("AnswerWordQuizAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task A_poll_answer_a_poll_takes_does_not_reach_the_word_quiz()
    {
        _fakes.Handlers["AnswerAsync"] = _ => Task.FromResult(true);

        await AnswerHandler().HandleAsync(Answer(), Context(), Ct);

        _fakes.Log.Of("AnswerWordQuizAsync").Should().BeEmpty();
    }

    [Fact]
    public void Start_and_reject_carry_the_poll_id()
    {
        var payload = PacketHarness.Payload(w => w.Int(POLL_ID));

        PacketHarness
            .Parse(PacketHarness.Incoming("PollStartMessageEvent"), payload)
            .Should()
            .BeEquivalentTo(new PollStartMessage { PollId = POLL_ID });
        PacketHarness
            .Parse(PacketHarness.Incoming("PollRejectMessageEvent"), payload)
            .Should()
            .BeEquivalentTo(new PollRejectMessage { PollId = POLL_ID });
    }

    [Fact]
    public void The_contents_are_written_in_the_order_the_client_reads_them()
    {
        ImmutableArray<RoomPollChoiceSnapshot> choices =
        [
            new()
            {
                Value = "1",
                Text = "Yes",
                ChoiceType = 0,
            },
        ];
        var packet = PacketHarness.Encode(
            new PollContentsEventMessageComposer
            {
                PollId = POLL_ID,
                StartMessage = "start",
                EndMessage = "end",
                NpsPoll = true,
                Questions =
                [
                    Snapshot(SINGLE, RoomPollQuestionType.SingleChoice, choices) with
                    {
                        Children = [Snapshot(FOLLOW_UP, RoomPollQuestionType.TextArea, [])],
                    },
                ],
            }
        );

        packet.PopInt().Should().Be(POLL_ID);
        packet.PopString().Should().Be("start");
        packet.PopString().Should().Be("end");
        packet.PopInt().Should().Be(1);
        ReadQuestion(packet, SINGLE, 1, 1);
        packet.PopString().Should().Be("1");
        packet.PopString().Should().Be("Yes");
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(1, "one follow-up");
        ReadQuestion(packet, FOLLOW_UP, 4, 0);
        packet.PopBoolean().Should().BeTrue();
        packet.Remaining.Should().Be(0);
    }

    private static void ReadQuestion(ClientPacket packet, int id, int type, int choices)
    {
        packet.PopInt().Should().Be(id);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(type);
        packet.PopString().Should().Be("q");
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(choices);
    }

    private static RoomPollQuestionSnapshot Snapshot(
        int id,
        RoomPollQuestionType type,
        ImmutableArray<RoomPollChoiceSnapshot> choices
    ) =>
        new()
        {
            QuestionId = id,
            SortOrder = 0,
            QuestionType = type,
            Text = "q",
            Category = 0,
            AnswerType = 0,
            Choices = choices,
            Children = [],
        };

    private IPlayerRoomPollGrain Grain() =>
        (IPlayerRoomPollGrain)GrainHarness.Create<PlayerRoomPollGrain>(_fakes, _db);

    private List<(int, string)> Answers()
    {
        using var ctx = _db.CreateDbContext();
        return
        [
            .. ctx
                .RoomPollAnswers.AsNoTracking()
                .AsEnumerable()
                .Select(x => (x.QuestionId, x.Answer)),
        ];
    }

    private RoomPollState? State()
    {
        using var ctx = _db.CreateDbContext();
        return ctx.PlayerRoomPolls.AsNoTracking().SingleOrDefault()?.State;
    }

    private PollAnswerMessageHandler AnswerHandler() => new(_fakes.Create<IGrainFactory>());

    private static PollAnswerMessage Answer() =>
        new()
        {
            PollId = POLL_ID,
            QuestionId = SINGLE,
            Answers = ["1"],
        };

    private MessageContext Context() => new(_fakes.Create<ISessionContext>(), 1, ROOM_ID);

    private static RoomPollQuestionEntity Question(
        int id,
        int? parent,
        int sortOrder,
        RoomPollQuestionType type,
        string text
    ) =>
        new()
        {
            Id = id,
            PollId = POLL_ID,
            ParentQuestionId = parent,
            SortOrder = sortOrder,
            QuestionType = type,
            Text = text,
            Category = parent is null ? 0 : 1,
            AnswerType = 0,
        };

    private static RoomPollChoiceEntity Choice(
        int id,
        int questionId,
        string value,
        string text,
        int choiceType
    ) =>
        new()
        {
            Id = id,
            QuestionId = questionId,
            SortOrder = id,
            Value = value,
            Text = text,
            ChoiceType = choiceType,
        };
}
