using System;
using System.Linq;
using System.Text.RegularExpressions;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Commands;

/// <summary>
/// Reads a command's attributes and checks it is fit to register. A command without a permission
/// is refused so <c>:commands</c> can list exactly what an executor holds, and nobody has to
/// remember to gate one.
/// </summary>
internal static partial class CommandDescriptorFactory
{
    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex NamePattern();

    public static CommandDescriptor Create(
        ICommand command,
        CommandArgumentParserRegistry? parsers = null
    )
    {
        var type = command.GetType();
        var attribute = type.GetCustomAttributes(typeof(CommandAttribute), inherit: false)
            .Cast<CommandAttribute>()
            .FirstOrDefault();

        if (attribute is null)
            throw new InvalidOperationException($"Command {type.Name} has no [Command] attribute.");

        ValidateName(type, attribute.Name);

        foreach (var alias in attribute.Aliases)
            ValidateName(type, alias);

        var nodes = type.GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: false)
            .Cast<RequiresPermissionAttribute>()
            .SelectMany(x => x.Nodes)
            .ToArray();

        if (nodes.Length == 0)
            throw new InvalidOperationException(
                $"Command '{attribute.Name}' ({type.Name}) has no [RequiresPermission]; every command declares the node that lets a player use it."
            );

        var level = type.GetCustomAttributes(typeof(RequiresRoomLevelAttribute), inherit: false)
            .Cast<RequiresRoomLevelAttribute>()
            .FirstOrDefault();

        if (level is not null && command is IOperatorCommand)
            throw new InvalidOperationException(
                $"Command '{attribute.Name}' ({type.Name}) is an operator command, which runs outside a room and has no room level to require."
            );

        return new CommandDescriptor
        {
            Name = attribute.Name,
            Aliases = attribute.Aliases,
            Description = attribute.Description,
            Category = string.IsNullOrWhiteSpace(attribute.Category)
                ? CommandCategories.GENERAL
                : attribute.Category.Trim(),
            Nodes = nodes,
            MinimumRoomLevel = level?.Level,
            Type = type,
            Command = command,
            Binder = new ArgumentsBinder(
                attribute.Name,
                command.ArgumentsType,
                command is IOperatorCommand,
                parsers
            ),
            Texts = command.DefaultTexts,
        };
    }

    private static void ValidateName(Type type, string name)
    {
        if (!NamePattern().IsMatch(name))
            throw new InvalidOperationException(
                $"Command {type.Name} name '{name}' must be lowercase letters, digits and underscores, starting with a letter."
            );
    }
}
