using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Rooms.Commands;

public sealed record WordQuizArguments(RestOfLine Question);

/// <summary>
/// <c>:wordquiz &lt;question&gt;</c>. Asks everyone in the room a thumbs up or down question in
/// the client's word quiz (Flash <c>WordQuizWidget</c>); answers show over the avatars and the
/// counts close the question when its time is up. Official Habbo runs these in its public rooms;
/// this is how a hotel's staff start one.
/// </summary>
[Command(
    "wordquiz",
    Description = "Ask everyone in the room a thumbs up or down question",
    Category = CommandCategories.ANNOUNCEMENTS
)]
[RequiresPermission(PermissionNodes.Command.WORDQUIZ)]
public sealed class WordQuizCommand : ICommand<WordQuizArguments>
{
    private const string ASKED = "asked";
    private const string BUSY = "busy";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [ASKED] = "Question asked.",
            [BUSY] = "A question is still open in this room.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        WordQuizArguments arguments,
        CancellationToken ct
    ) =>
        await ctx.Room.StartWordQuizAsync(arguments.Question.Text, ct)
            ? CommandResult.Done(ASKED)
            : CommandResult.Fail(BUSY);
}
