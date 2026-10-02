using System;
using System.Linq;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.Commands;

/// <summary>
/// The commands one player may use, as <c>chat.commands</c> sends them. A pure function of the
/// registry, what the player holds and the hotel texts, so it is tested without a grain. A
/// command is in it exactly when <c>:commands</c> would list it in a room where the player has
/// every controller level: the client hides one whose room level it does not reach there.
/// </summary>
public static class CommandTreeBuilder
{
    public static CommandTreeSnapshot Build(
        ICommandRegistry registry,
        ResolvedPermissionsSnapshot permissions,
        IHotelTextProvider texts
    ) =>
        new()
        {
            Commands =
            [
                .. registry
                    .Commands.Where(command => MayUse(command, permissions))
                    .OrderBy(command => command.Name, StringComparer.Ordinal)
                    .Select(command => Entry(registry, command, permissions, texts)),
            ],
        };

    /// <summary>
    /// What a tree for these permissions depends on: the commands the player may use, and the
    /// selectors among them. Two equal keys build the same tree, so a permission change that
    /// leaves the key as it was sends nothing.
    /// </summary>
    public static string Key(ICommandRegistry registry, ResolvedPermissionsSnapshot permissions) =>
        string.Join(
            ',',
            registry
                .Commands.Where(command => MayUse(command, permissions))
                .Select(command =>
                    (
                        command.SelectorNode is { } node && permissions.Has(node)
                            ? command.Name + "@"
                            : command.Name
                    )
                    + ":"
                    + string.Join(
                        "/",
                        command
                            .Binder.Syntax.Where(x =>
                                x.Permission is null || permissions.Has(x.Permission)
                            )
                            .Select(x => x.Path)
                    )
                )
                .Order(StringComparer.Ordinal)
        );

    public static bool MayUse(CommandDescriptor command, ResolvedPermissionsSnapshot permissions) =>
        command.Nodes.Any(permissions.Has)
        && (
            command.Binder.Syntax.Count == 0
            || command.Binder.Syntax.Any(x => x.Permission is null || permissions.Has(x.Permission))
        );

    private static CommandTreeEntrySnapshot Entry(
        ICommandRegistry registry,
        CommandDescriptor command,
        ResolvedPermissionsSnapshot permissions,
        IHotelTextProvider texts
    ) =>
        new()
        {
            Name = command.Name,
            Aliases = [.. registry.AliasesOf(command)],
            Category = command.Category,
            Description = texts.TryGetText(
                CommandReplyKeys.ForCommand(command.Name, CommandReplyKeys.DESCRIPTION),
                out var description
            )
                ? description
                : command.Description,
            Usage =
                command.Binder.Syntax.Count == 0
                    ? command.Usage
                    : string.Join(
                        " | ",
                        command
                            .Binder.Syntax.Where(x =>
                                x.Permission is null || permissions.Has(x.Permission)
                            )
                            .Select(x => x.Binder.Usage)
                    ),
            RoomLevel = command.MinimumRoomLevel is { } level ? (int)level : -1,
            Operator = command.IsOperator,
            Parameters = Parameters(command.Binder, permissions),
            Syntax =
            [
                .. command
                    .Binder.Syntax.Where(x => x.Permission is null || permissions.Has(x.Permission))
                    .Select(x => new CommandSyntaxSnapshot
                    {
                        Path = x.Path,
                        Usage = x.Binder.Usage,
                        Parameters = Parameters(x.Binder, permissions),
                    }),
            ],
        };

    private static System.Collections.Immutable.ImmutableArray<CommandTreeParameterSnapshot> Parameters(
        ICommandBinder binder,
        ResolvedPermissionsSnapshot permissions
    ) =>
        [
            .. binder.Parameters.Select(parameter => new CommandTreeParameterSnapshot
            {
                Name = parameter.Name,
                Kind = parameter.Kind,
                Optional = parameter.Optional,
                Suggest = parameter.Suggest,
                Members = parameter.Members,
                Selectors = parameter.SelectorNode is { } node && permissions.Has(node),
                Description = parameter.Description,
                Minimum = parameter.Minimum ?? string.Empty,
                Maximum = parameter.Maximum ?? string.Empty,
                MinLength = parameter.MinLength,
                MaxLength = parameter.MaxLength,
                DefaultValue = parameter.DefaultValue,
            }),
        ];
}
