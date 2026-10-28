using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Testing;

/// <summary>
/// A <c>:command</c> said in a <see cref="TestRoom"/>, run in the order the room runs one
/// (turbo-cloud <c>RoomCommandSystem</c>): a node it needs, the room level it needs, its arguments
/// bound by the hotel's own binder, then the command, and its result whispered. An operator
/// command said by a holder of its node leaves the room for the hotel's own runner
/// (<c>OperatorCommandRunner</c>), which does the rest as on a server: its arguments, the
/// plugins' vetoes, the command, <c>:confirm</c>, its reply and the command log. The plugins'
/// vetoes of a room command are not run here.
/// </summary>
internal static class TestCommands
{
    public static async Task RunAsync(
        TestHotel hotel,
        TestPlayer speaker,
        TestRoom room,
        string text,
        CancellationToken ct
    )
    {
        if (!TryMatch(hotel, text, out var descriptor, out var argumentText))
        {
            room.Say(speaker.Name, text, RoomSpeech.Talk);

            return;
        }

        var holdsANode = descriptor.Nodes.Any(node => hotel.Grants(speaker.Fake, node));

        if (descriptor.IsOperator && holdsANode)
        {
            speaker.LastCommand = await hotel
                .OperatorCommands.RunAsync(
                    descriptor,
                    new TestOperatorExecutor(hotel, speaker, room),
                    argumentText,
                    checkNode: false,
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        var executor = Avatar(hotel, speaker);
        var scope = new CommandRoom(hotel, room, speaker);

        if (!holdsANode)
        {
            await ReplyAsync(
                    hotel,
                    speaker,
                    room,
                    descriptor,
                    CommandReplyKeys.NO_PERMISSION,
                    [],
                    ct
                )
                .ConfigureAwait(false);
            speaker.LastCommand = CommandOutcome.Refused;

            return;
        }

        if (
            descriptor.MinimumRoomLevel is { } required
            && await scope.GetControllerLevelAsync(executor).ConfigureAwait(false) < required
        )
        {
            await ReplyAsync(
                    hotel,
                    speaker,
                    room,
                    descriptor,
                    CommandReplyKeys.NEEDS_ROOM_LEVEL,
                    [],
                    ct
                )
                .ConfigureAwait(false);
            speaker.LastCommand = CommandOutcome.RoomLevel;

            return;
        }

        var bound = descriptor.Binder.Bind(
            argumentText,
            scope,
            node => hotel.Grants(speaker.Fake, node)
        );

        if (bound.RequiredPermission is { } branch && !hotel.Grants(speaker.Fake, branch))
        {
            await ReplyAsync(
                    hotel,
                    speaker,
                    room,
                    descriptor,
                    CommandReplyKeys.NO_PERMISSION,
                    [],
                    ct
                )
                .ConfigureAwait(false);
            speaker.LastCommand = CommandOutcome.Refused;

            return;
        }

        if (!bound.Succeeded)
        {
            await ReplyAsync(
                    hotel,
                    speaker,
                    room,
                    descriptor,
                    bound.ErrorKey!,
                    bound.Parameters,
                    ct
                )
                .ConfigureAwait(false);
            speaker.LastCommand = CommandOutcome.BindFailed;

            return;
        }

        var result = await descriptor
            .Command.ExecuteAsync(
                new CommandContext(
                    ActionContext.CreateForPlayer(speaker.Id, room.Id) with
                    {
                        SessionKey = speaker.SessionKey,
                    },
                    executor,
                    room.Id,
                    scope,
                    argumentText
                ),
                bound.Arguments!,
                ct
            )
            .ConfigureAwait(false);

        if (result.Status is { } status)
            await ReplyAsync(
                    hotel,
                    speaker,
                    room,
                    descriptor,
                    CommandReplyKeys.IsShared(status)
                        ? status
                        : CommandReplyKeys.ForCommand(descriptor.Name, status),
                    result.Parameters,
                    ct,
                    status
                )
                .ConfigureAwait(false);

        speaker.LastCommand = result.Outcome;
    }

    private static bool TryMatch(
        TestHotel hotel,
        string text,
        out CommandDescriptor descriptor,
        out string argumentText
    )
    {
        descriptor = null!;
        argumentText = string.Empty;

        if (text.Length < 2 || text[0] != ':')
            return false;

        var end = 1;

        while (end < text.Length && !char.IsWhiteSpace(text[end]))
            end++;

        if (!hotel.Commands.Current.TryFind(text.AsSpan(1, end - 1), out descriptor!))
            return false;

        argumentText = text[end..];

        return true;
    }

    /// <summary>The reply as the room words it: the hotel's text, else core's default, else the command's own.</summary>
    private static async Task ReplyAsync(
        TestHotel hotel,
        TestPlayer speaker,
        TestRoom room,
        CommandDescriptor descriptor,
        string key,
        string[] parameters,
        CancellationToken ct,
        string? status = null
    )
    {
        var text = hotel.Plugins.Values.Select(p => p.Text(key)).FirstOrDefault(t => t is not null);

        if (
            text is null
            && !CommandReplyKeys.Defaults.TryGetValue(key, out text)
            && !(status is not null && descriptor.Texts.TryGetValue(status, out text))
        )
            return;

        for (var i = 0; i < parameters.Length; i++)
            text = text.Replace($"%{i}%", parameters[i], StringComparison.Ordinal);

        await hotel.Fake.Rooms.WhisperAsync(room.Id, speaker.Id, text, ct).ConfigureAwait(false);
    }

    /// <summary>The player's avatar as a command reads it: their id, name, gender, score and nodes.</summary>
    internal static IRoomPlayer Avatar(TestHotel hotel, TestPlayer player)
    {
        hotel.Sessions.Answer<IRoomPlayer>(
            "get_PlayerId",
            call => hotel.PlayerOf(TestPlayer.PlayerOf(call.Key)).Id
        );
        hotel.Sessions.Answer<IRoomPlayer>(
            "get_Name",
            call => hotel.PlayerOf(TestPlayer.PlayerOf(call.Key)).Name
        );
        hotel.Sessions.Answer<IRoomPlayer>(
            "get_Gender",
            call => hotel.PlayerOf(TestPlayer.PlayerOf(call.Key)).Fake.Gender
        );
        hotel.Sessions.Answer<IRoomPlayer>(
            "get_Figure",
            call => hotel.PlayerOf(TestPlayer.PlayerOf(call.Key)).Fake.Figure
        );
        hotel.Sessions.Answer<IRoomPlayer>(
            "get_AchievementScore",
            call => (int)hotel.PlayerOf(TestPlayer.PlayerOf(call.Key)).Fake.AchievementScore
        );
        hotel.Sessions.Answer<IRoomPlayer>(
            "get_Permissions",
            call =>
                ResolvedPermissionsSnapshot.EMPTY with
                {
                    Granted = [.. hotel.PlayerOf(TestPlayer.PlayerOf(call.Key)).Fake.Permissions],
                }
        );

        return hotel.Sessions.Of<IRoomPlayer>(player.Id.Value);
    }

    private sealed class CommandContext(
        ActionContext action,
        IRoomPlayer executor,
        RoomId roomId,
        ICommandRoom room,
        string argumentText
    ) : ICommandContext
    {
        public ActionContext Action => action;

        public IRoomPlayer Executor => executor;

        public RoomId RoomId => roomId;

        public ICommandRoom Room => room;

        public string ArgumentText => argumentText;
    }

    /// <summary>
    /// The room a command runs in: who is here, whispers and lines said (kept by the room), the
    /// room level (its owner's is <see cref="RoomControllerType.Owner"/>, a player holding
    /// <c>*</c> a moderator's, anyone else none). What it would change of the room is recorded in
    /// <see cref="TestRoom.Actions"/> and answered as done.
    /// </summary>
    private sealed class CommandRoom(TestHotel hotel, TestRoom room, TestPlayer executor)
        : ICommandRoom
    {
        public IEnumerable<IRoomPlayer> Players => room.Players.Select(p => Avatar(hotel, p));

        public IRoomPlayer? FindPlayer(string name) =>
            room.Players.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            )
                is { } player
                ? Avatar(hotel, player)
                : null;

        public Task<RoomControllerType> GetControllerLevelAsync(IRoomPlayer player)
        {
            var who = hotel.PlayerOf(player.PlayerId);

            return Task.FromResult(
                who.Fake.Permissions.Contains("*") ? RoomControllerType.Moderator
                : room.Fake.Owner.Id == who.Id ? RoomControllerType.Owner
                : RoomControllerType.None
            );
        }

        public Task WhisperAsync(IRoomPlayer player, string text, CancellationToken ct) =>
            hotel.Fake.Rooms.WhisperAsync(room.Id, player.PlayerId, text, ct);

        public Task SayAsync(IRoomPlayer speaker, string text, bool shout, CancellationToken ct)
        {
            room.Say(speaker.Name, text, new RoomSpeech { Shout = shout });

            return Task.CompletedTask;
        }

        public IReadOnlyList<IRoomFloorItem> FloorItemsAt(int x, int y) => [];

        public Task NoticeAsync(
            IRoomPlayer player,
            IReadOnlyList<string> lines,
            CancellationToken ct
        )
        {
            room.Act($"Notice({player.Name}: {string.Join(" | ", lines)})");

            return Task.CompletedTask;
        }

        public Task<bool> KickAsync(IRoomPlayer target, CancellationToken ct) =>
            DoneAsync($"Kick({target.Name})", true);

        public Task<bool> MuteAsync(IRoomPlayer target, int minutes, CancellationToken ct) =>
            DoneAsync($"Mute({target.Name}, {minutes})", true);

        public Task<int> ClearAsync(CancellationToken ct) => DoneAsync("Clear()", 0);

        public Task<bool> SetRoomMutedAsync(bool muted, CancellationToken ct) =>
            DoneAsync($"SetRoomMuted({muted})", true);

        public Task<int> PickUpAllOwnedAsync(bool buildersClub, CancellationToken ct) =>
            DoneAsync($"PickUpAllOwned({buildersClub})", 0);

        public Task<int?> EjectAllAsync(CancellationToken ct) => DoneAsync("EjectAll()", (int?)0);

        public Task<int?> EjectPetsAsync(CancellationToken ct) => DoneAsync("EjectPets()", (int?)0);

        public Task<int?> ResetScoresAsync(CancellationToken ct) =>
            DoneAsync("ResetScores()", (int?)0);

        public bool TogglePetsMuted()
        {
            room.Act("TogglePetsMuted()");

            return true;
        }

        public bool ToggleBotsMuted()
        {
            room.Act("ToggleBotsMuted()");

            return true;
        }

        public Task<bool> IsGroupRoomAsync(CancellationToken ct) => Task.FromResult(false);

        public Task PlayEffectAsync(SpecialRoomEffectType effect, CancellationToken ct) =>
            DoneAsync($"PlayEffect({effect})", true);

        public Task<bool> StartWordQuizAsync(string question, CancellationToken ct) =>
            DoneAsync($"StartWordQuiz({question})", true);

        public Task UnloadAsync(CancellationToken ct) => DoneAsync("Unload()", true);

        private Task<T> DoneAsync<T>(string action, T answer)
        {
            room.Act(action);

            return Task.FromResult(answer);
        }

        public override string ToString() => $"{room} for {executor.Name}";
    }
}
