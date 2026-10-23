using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Skills;

/// <summary><c>:skills</c>: the player's level, in reply. Found as the plugin loads.</summary>
[Command("skills", Description = "Show your skill level")]
[RequiresPermission(SkillsPermissions.COMMAND)]
public sealed class SkillsCommand(SkillBook book) : ICommand<SkillsCommand.Arguments>
{
    public const string LEVEL = "level";

    public sealed record Arguments;

    /// <summary>Used while the hotel's texts have no <c>command.skills.level</c>.</summary>
    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [LEVEL] = "You are level %0% (%1%/%2% experience)." };

    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        Arguments arguments,
        CancellationToken ct
    )
    {
        var skill = book.Of(ctx.Executor.PlayerId);

        return ValueTask.FromResult(
            CommandResult.Done(
                LEVEL,
                skill.Level.ToString(),
                skill.Experience.ToString(),
                skill.Needed.ToString()
            )
        );
    }
}
