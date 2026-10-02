using System.Collections.Generic;

namespace Turbo.Primitives.Commands;

/// <summary>Turns the text after a command's name into its arguments record.</summary>
public interface ICommandBinder
{
    /// <summary>
    /// The usage line, built from the arguments record: <c>:kick &lt;who&gt; [why]</c>.
    /// </summary>
    string Usage { get; }

    /// <summary>
    /// The node that lets an executor aim the command at <c>@room</c> or <c>@online</c>, from the
    /// <see cref="SelectorsAttribute"/> on its target; null when it takes no selector.
    /// </summary>
    string? SelectorNode { get; }

    /// <summary>The name of the parameter that takes a selector, as the usage line spells it.</summary>
    string? SelectorParameter { get; }

    /// <summary>The parameters, in order, as read from the arguments record.</summary>
    IReadOnlyList<CommandParameterInfo> Parameters { get; }

    IReadOnlyList<CommandSyntax> Syntax => [];

    /// <summary>Arguments safe to retain in the audit, even when input is malformed.</summary>
    string AuditText(string argumentText) => argumentText;

    /// <summary>
    /// Binds the text. <paramref name="room"/> is null for an operator command, which runs outside
    /// any room; a parameter that needs the room is then refused.
    /// </summary>
    CommandBindResult Bind(
        string argumentText,
        ICommandRoom? room,
        System.Func<string, bool>? hasPermission = null
    );
}
