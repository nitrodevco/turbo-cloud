using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.Rooms.Commands;

/// <summary>
/// Lists what the executor may use here, in a scrollable notice: a node they hold and a room
/// level they have. Commands are grouped under their category, General first and the rest
/// alphabetically, and alphabetical within each, so a player finds one without reading the lot.
/// Each category is one item of the notice, a <c>---- NAME ----</c> heading with its commands on
/// the lines straight under it, because the window pads every item and that padding is the gap
/// between categories.
/// It is the one command everybody holds, so a player who has forgotten a name can always ask.
/// </summary>
[Command("commands", Aliases = ["help"], Description = "List the commands you can use")]
[RequiresPermission(PermissionNodes.Command.COMMANDS)]
public sealed class CommandsCommand(
    ICommandRegistryProvider registryProvider,
    IHotelTextProvider? texts = null
) : ICommand<CommandsArguments>
{
    /// <summary>The client's text splits paragraphs on a line feed, not a carriage return.</summary>
    public const char LINE_BREAK = (char)10;

    public async ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        CommandsArguments arguments,
        CancellationToken ct
    )
    {
        var level = await ctx.Room.GetControllerLevelAsync(ctx.Executor);
        var sections = new List<string>();
        if (arguments.Command is { } name)
        {
            if (
                !registryProvider.Current.TryFind(name.TrimStart(':'), out var command)
                || !CommandTreeBuilder.MayUse(command, ctx.Executor.Permissions)
                || (command.MinimumRoomLevel is { } required && level < required)
            )
                return CommandResult.Fail(CommandReplyKeys.NO_PERMISSION);
            await ctx.Room.NoticeAsync(
                ctx.Executor,
                CommandHelp.Describe(
                    registryProvider.Current,
                    command,
                    ctx.Executor.Permissions.Has,
                    texts,
                    details: true
                ),
                ct
            );
            return CommandResult.Ok;
        }

        foreach (
            var section in registryProvider
                .Current.Commands.Where(command =>
                    CommandTreeBuilder.MayUse(command, ctx.Executor.Permissions)
                    && (command.MinimumRoomLevel is not { } required || level >= required)
                )
                .GroupBy(command => command.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(section => section.Key == CommandCategories.GENERAL ? 0 : 1)
                .ThenBy(section => section.Key, StringComparer.OrdinalIgnoreCase)
        )
        {
            var lines = new List<string> { $"---- {section.Key.ToUpperInvariant()} ----" };

            lines.AddRange(
                section
                    .OrderBy(command => command.Name, StringComparer.Ordinal)
                    .SelectMany(command =>
                        CommandHelp.Describe(
                            registryProvider.Current,
                            command,
                            ctx.Executor.Permissions.Has,
                            texts
                        )
                    )
            );

            sections.Add(string.Join(LINE_BREAK, lines));
        }

        await ctx.Room.NoticeAsync(ctx.Executor, sections, ct);

        return CommandResult.Ok;
    }
}
