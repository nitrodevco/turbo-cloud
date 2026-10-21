using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Players.Permissions;
using Turbo.Rooms.Commands;
using Turbo.Rooms.Grains.Systems;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// The room's word quiz (Flash WordQuizWidget): staff ask a question, everyone in the room answers
/// once with a thumb up or down, each answer is shown to the room with the counts so far, and the
/// counts close the question when its time is up. The packets are written as the official client
/// capture of 2026-10-12 (session-20261012-102507) shows them.
/// </summary>
public class WordQuizTests
{
    private const int STAFF = 2;
    private const int GUEST = 3;
    private const int OUTSIDER = 4;

    private readonly CommandRoomFixture _room = new();

    public WordQuizTests() => _room.Commands.Register([new WordQuizCommand()]);

    [Fact]
    public async Task A_question_takes_one_answer_each_and_closes_with_the_counts()
    {
        _room.AddPlayer(STAFF, PermissionNodes.Command.WORDQUIZ);
        _room.AddPlayer(GUEST);

        await _room.SayAsync(STAFF, ":wordquiz Vanilla is my jam...");
        await _room.SayAsync(GUEST, ":wordquiz Me too?");

        var question = _room
            .Harness.Fakes.SentToRoom<QuestionEventMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;

        question
            .Should()
            .Be(
                new QuestionEventMessageComposer
                {
                    PollType = "MATCHING_POLL",
                    PollId = _room.Harness.Room.RoomId.Value,
                    QuestionId = 1,
                    DurationMs = 15000,
                    Number = 1,
                    QuestionType = 6,
                    Content = "Vanilla is my jam...",
                }
            );
        _room.WhispersTo(STAFF).Should().Equal("Question asked.");
        _room.WhispersTo(GUEST).Should().Equal("You can't use that command.");

        (await AnswerAsync(GUEST, question, "1")).Should().BeTrue();
        (await AnswerAsync(GUEST, question, "0")).Should().BeFalse("one answer each");
        (await AnswerAsync(STAFF, question, "maybe")).Should().BeFalse();
        (await AnswerAsync(OUTSIDER, question, "1")).Should().BeFalse("not in the room");
        (await AnswerAsync(STAFF, question with { QuestionId = 9 }, "1")).Should().BeFalse();
        (await AnswerAsync(STAFF, question, "0")).Should().BeTrue();

        _room
            .Harness.Fakes.SentToRoom<QuestionAnsweredEventMessageComposer>()
            .Select(x => (x.UserId, x.Value, Counts(x.AnswerCounts)))
            .Should()
            .Equal((GUEST, "1", "1=1"), (STAFF, "0", "1=1,0=1"));

        await _room.SayAsync(STAFF, ":wordquiz Another?");
        _room.WhispersTo(STAFF).Last().Should().Be("A question is still open in this room.");

        await _room
            .Harness.Module<RoomTimerSystem>()
            .ProcessTimersAsync(long.MaxValue / 2, CancellationToken.None);

        var finished = _room
            .Harness.Fakes.SentToRoom<QuestionFinishedEventMessageComposer>()
            .Single();

        (finished.QuestionId, Counts(finished.AnswerCounts)).Should().Be((1, "1=1,0=1"));
        (await AnswerAsync(GUEST, question, "1")).Should().BeFalse("the question is closed");
    }

    [Fact]
    public void Quiz_packets_are_written_and_read_as_the_official_client_capture_shows()
    {
        var question = PacketHarness.Encode(
            new QuestionEventMessageComposer
            {
                PollType = "MATCHING_POLL",
                PollId = 2537,
                QuestionId = 11203,
                DurationMs = 15000,
                Number = 38,
                QuestionType = 6,
                Content = "Vanilla is my jam...",
            }
        );
        var finished = PacketHarness.Encode(
            new QuestionFinishedEventMessageComposer { QuestionId = 11203, AnswerCounts = [] }
        );
        var answered = PacketHarness.Encode(
            new QuestionAnsweredEventMessageComposer
            {
                UserId = 7,
                Value = "1",
                AnswerCounts = [new("1", 2)],
            }
        );

        // The capture's Question, less the 16 bytes after the content that Flash's QuestionParser
        // never reads for a type 6 question.
        Convert
            .ToHexString(question.PopBytes(question.Remaining))
            .Should()
            .Be(
                "000D4D41544348494E475F504F4C4C000009E900002BC300003A9800002BC30000002600000006001456616E696C6C61206973206D79206A616D2E2E2E"
            );
        Convert.ToHexString(finished.PopBytes(finished.Remaining)).Should().Be("00002BC300000000");
        // QuestionAnsweredParser.as:37: user id, value, then each value with its count.
        Convert
            .ToHexString(answered.PopBytes(answered.Remaining))
            .Should()
            .Be("00000007" + "000131" + "00000001" + "000131" + "00000002");

        PacketHarness
            .Parse(
                PacketHarness.Incoming("PollAnswerMessageEvent"),
                PacketHarness.Payload(w => w.Int(2537).Int(11203).Int(1).String("1"))
            )
            .Should()
            .BeEquivalentTo(
                new PollAnswerMessage
                {
                    PollId = 2537,
                    QuestionId = 11203,
                    Answers = ["1"],
                }
            );
    }

    private Task<bool> AnswerAsync(
        int player,
        QuestionEventMessageComposer question,
        string value
    ) =>
        _room.Harness.Room.AnswerWordQuizAsync(
            ActionContext.CreateForPlayer(player, _room.Harness.Room.RoomId),
            question.PollId,
            question.QuestionId,
            [value],
            CancellationToken.None
        );

    private static string Counts(IReadOnlyList<KeyValuePair<string, int>> counts) =>
        string.Join(",", counts.Select(x => $"{x.Key}={x.Value}"));
}
