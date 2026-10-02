using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Rooms.Object.Avatars;
using Kind = Turbo.Primitives.Commands.Enums.CommandParameterKind;

namespace Turbo.Commands;

/// <summary>
/// Binds the text after a command's name to its arguments record. Everything that needs
/// reflection happens once, when the command is registered: the parameters are read from the
/// record's primary constructor, the usage line is built, and the constructor is compiled. A line
/// then costs a walk over its words and one constructor call.
/// </summary>
internal sealed class ArgumentsBinder : ICommandBinder
{
    private sealed record Parameter(
        string Name,
        Kind Kind,
        Type ValueType,
        bool Optional,
        object? DefaultValue,
        string? SelectorNode,
        string? SuggestionSource,
        CommandParameterAttribute Limits,
        ICommandArgumentParser? Parser
    );

    private readonly Parameter[] _parameters;
    private readonly Func<object?[], object> _construct;

    public string Usage { get; }

    public string? SelectorNode { get; }

    public string? SelectorParameter { get; }

    public IReadOnlyList<CommandParameterInfo> Parameters { get; }

    public IReadOnlyList<CommandSyntax> Syntax { get; } = [];

    public string AuditText(string argumentText) =>
        Parameters.Any(x => x.Sensitive)
        || Syntax.Any(x => x.Binder.AuditText(argumentText) == "[redacted]")
            ? "[redacted]"
            : argumentText;

    public ArgumentsBinder(
        string commandName,
        Type argumentsType,
        bool isOperator,
        CommandArgumentParserRegistry? parsers = null
    )
    {
        var branches = argumentsType.GetCustomAttributes<CommandBranchAttribute>(false).ToArray();
        if (branches.Length > 0)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var syntax = new List<CommandSyntax>();
            foreach (var branch in branches)
            {
                if (
                    !argumentsType.IsAssignableFrom(branch.ArgumentsType)
                    || branch.ArgumentsType == argumentsType
                    || !System.Text.RegularExpressions.Regex.IsMatch(
                        branch.Path,
                        "^[a-z][a-z0-9_]*( [a-z][a-z0-9_]*)*$"
                    )
                    || !paths.Add(branch.Path)
                )
                    throw new InvalidOperationException(
                        $"Command '{commandName}' has an invalid or duplicate branch '{branch.Path}'."
                    );
                var binder = new ArgumentsBinder(
                    commandName + " " + branch.Path,
                    branch.ArgumentsType,
                    isOperator,
                    parsers
                );
                if (binder.Syntax.Count > 0)
                    throw new InvalidOperationException(
                        "Declare complete literal paths on the root arguments record."
                    );
                syntax.Add(new CommandSyntax(branch.Path, binder, branch.Permission));
            }
            Syntax = syntax;
            _parameters = [];
            _construct = _ => throw new InvalidOperationException("A branch must be selected.");
            Parameters = [];
            Usage = string.Join(" | ", syntax.Select(x => x.Binder.Usage));
            var selectors = syntax
                .Select(x => x.Binder.SelectorNode)
                .Where(x => x is not null)
                .Distinct()
                .ToArray();
            if (selectors.Length > 1)
                throw new InvalidOperationException(
                    "Command branches must share their selector permission."
                );
            SelectorNode = selectors.FirstOrDefault();
            SelectorParameter = syntax
                .Select(x => x.Binder.SelectorParameter)
                .FirstOrDefault(x => x is not null);
            return;
        }
        var constructor = argumentsType
            .GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .FirstOrDefault();

        if (constructor is null)
            throw new InvalidOperationException(
                $"Command '{commandName}': arguments type {argumentsType.Name} has no public constructor."
            );

        var nullability = new NullabilityInfoContext();
        var parameters = constructor.GetParameters();

        _parameters = new Parameter[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            _parameters[i] = Describe(commandName, parameters[i], nullability, parsers);

            // A room player is looked up in the room the command runs in, which an operator
            // command has no turn in; a target is looked up by name across the hotel, which a room
            // command has no way to wait for. Either is a mistake to catch at registration.
            if (isOperator && _parameters[i].Kind == Kind.RoomPlayer)
                throw new InvalidOperationException(
                    $"Command '{commandName}': operator commands take a PlayerTarget, not an IRoomPlayer ('{_parameters[i].Name}')."
                );

            if (!isOperator && _parameters[i].Kind == Kind.Player)
                throw new InvalidOperationException(
                    $"Command '{commandName}': only operator commands take a PlayerTarget ('{_parameters[i].Name}')."
                );

            if (_parameters[i].SelectorNode is { } selectorNode)
            {
                if (_parameters[i].Kind != Kind.Player)
                    throw new InvalidOperationException(
                        $"Command '{commandName}': [Selectors] is only for a PlayerTarget ('{_parameters[i].Name}')."
                    );

                if (SelectorNode is not null)
                    throw new InvalidOperationException(
                        $"Command '{commandName}': only one parameter may take a selector ('{_parameters[i].Name}')."
                    );

                SelectorNode = selectorNode;
                SelectorParameter = _parameters[i].Name.ToLowerInvariant();
            }

            if (_parameters[i].SuggestionSource is not null && _parameters[i].Kind != Kind.Word)
                throw new InvalidOperationException(
                    $"Command '{commandName}': [Suggest] is only for a string ('{_parameters[i].Name}')."
                );

            if (_parameters[i].Kind == Kind.Rest && i != parameters.Length - 1)
                throw new InvalidOperationException(
                    $"Command '{commandName}': RestOfLine '{_parameters[i].Name}' must be the last parameter."
                );

            if (i > 0 && !_parameters[i].Optional && _parameters[i - 1].Optional)
                throw new InvalidOperationException(
                    $"Command '{commandName}': required '{_parameters[i].Name}' follows an optional parameter."
                );
        }

        _construct = Compile(constructor);
        Usage = BuildUsage(commandName, _parameters);
        Parameters =
        [
            .. _parameters.Select(x => new CommandParameterInfo
            {
                Name = x.Name.ToLowerInvariant(),
                Kind = x.Kind,
                Optional = x.Optional,
                Members =
                    x.Kind == Kind.Enumeration
                        ? [.. Enum.GetNames(x.ValueType).Select(name => name.ToLowerInvariant())]
                        : [],
                SuggestionSource = x.SuggestionSource,
                SelectorNode = x.SelectorNode,
                Description = x.Limits.Description,
                Minimum =
                    x.Limits.Minimum
                    ?? (
                        x.Kind == Kind.Integer ? int.MinValue.ToString(CultureInfo.InvariantCulture)
                        : x.Kind == Kind.Long ? long.MinValue.ToString(CultureInfo.InvariantCulture)
                        : null
                    ),
                Maximum =
                    x.Limits.Maximum
                    ?? (
                        x.Kind == Kind.Integer ? int.MaxValue.ToString(CultureInfo.InvariantCulture)
                        : x.Kind == Kind.Long ? long.MaxValue.ToString(CultureInfo.InvariantCulture)
                        : null
                    ),
                MinLength = x.Limits.MinLength,
                MaxLength = x.Limits.MaxLength,
                DefaultValue =
                    x.Limits.Sensitive || !x.Optional
                        ? string.Empty
                        : Convert.ToString(x.DefaultValue, CultureInfo.InvariantCulture)
                            ?? string.Empty,
                Sensitive = x.Limits.Sensitive,
            }),
        ];
    }

    public CommandBindResult Bind(
        string argumentText,
        ICommandRoom? room,
        Func<string, bool>? hasPermission = null
    )
    {
        if (Syntax.Count > 0)
        {
            foreach (var branch in Syntax.OrderByDescending(x => x.Path.Length))
            {
                var cursor = 0;
                var matched = true;
                foreach (var literal in branch.Path.Split(' '))
                {
                    var token = CommandTextReader.Read(argumentText, ref cursor);
                    if (
                        !token.Valid
                        || !token.Text.Equals(literal, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        matched = false;
                        break;
                    }
                }
                if (!matched)
                    continue;
                if (
                    branch.Permission is { } permission
                    && hasPermission is not null
                    && !hasPermission(permission)
                )
                    return CommandBindResult.Failure(CommandReplyKeys.NO_PERMISSION) with
                    {
                        RequiredPermission = permission,
                    };
                var result = branch.Binder.Bind(argumentText[cursor..], room, hasPermission);
                return result with
                {
                    RequiredPermission = branch.Permission,
                    ErrorStart = result.ErrorStart < 0 ? -1 : result.ErrorStart + cursor,
                    ErrorEnd = result.ErrorEnd < 0 ? -1 : result.ErrorEnd + cursor,
                };
            }
            var visible = Syntax
                .Where(x =>
                    x.Permission is null || hasPermission is null || hasPermission(x.Permission)
                )
                .Select(x => x.Binder.Usage)
                .ToArray();
            return visible.Length == 0
                ? CommandBindResult.Failure(CommandReplyKeys.NO_PERMISSION)
                : CommandBindResult.Failure(CommandReplyKeys.USAGE, string.Join(" | ", visible));
        }
        var values = new object?[_parameters.Length];
        var position = 0;

        for (var i = 0; i < _parameters.Length; i++)
        {
            var parameter = _parameters[i];
            while (position < argumentText.Length && char.IsWhiteSpace(argumentText[position]))
                position++;

            if (position == argumentText.Length)
            {
                if (!parameter.Optional)
                    return CommandBindResult.Failure(CommandReplyKeys.USAGE, Usage) with
                    {
                        ErrorStart = position,
                        ErrorEnd = position,
                    };

                values[i] = parameter.DefaultValue;

                continue;
            }

            string token;
            var start = position;

            if (parameter.Kind == Kind.Rest)
            {
                token = argumentText[position..].TrimEnd();
                position = argumentText.Length;
            }
            else
            {
                var read = CommandTextReader.Read(argumentText, ref position);
                if (!read.Valid)
                    return CommandBindResult.Failure(
                        CommandReplyKeys.BAD_QUOTE,
                        (read.Start + 1).ToString(CultureInfo.InvariantCulture),
                        Usage
                    ) with
                    {
                        ErrorStart = read.Start,
                        ErrorEnd = read.End,
                    };
                token = read.Text;
            }

            var failure = TryParse(parameter, token, room, out var value);

            if (failure is not null)
                return failure.Value with { ErrorStart = start, ErrorEnd = position };

            var info = Parameters[i];
            if (
                (info.MinLength >= 0 && token.Length < info.MinLength)
                || (info.MaxLength >= 0 && token.Length > info.MaxLength)
                || (
                    (info.Kind == Kind.Integer || info.Kind == Kind.Long)
                    && long.TryParse(
                        token,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var number
                    )
                    && (
                        (
                            info.Minimum is { } min
                            && number < long.Parse(min, CultureInfo.InvariantCulture)
                        )
                        || (
                            info.Maximum is { } max
                            && number > long.Parse(max, CultureInfo.InvariantCulture)
                        )
                    )
                )
            )
                return CommandBindResult.Failure(
                    CommandReplyKeys.CONSTRAINT,
                    info.Sensitive ? "[redacted]" : token,
                    info.Name,
                    info.Kind == Kind.Integer || info.Kind == Kind.Long
                        ? $"{info.Minimum}..{info.Maximum}"
                        : $"length {info.MinLength}..{info.MaxLength}"
                ) with
                {
                    ErrorStart = start,
                    ErrorEnd = position,
                };

            values[i] = value;
        }

        // A line with more words than the command has parameters is a mistake, not a bonus.
        if (!argumentText.AsSpan(position).Trim().IsEmpty)
            return CommandBindResult.Failure(CommandReplyKeys.USAGE, Usage) with
            {
                ErrorStart = position,
                ErrorEnd = argumentText.Length,
            };

        return CommandBindResult.Success(_construct(values));
    }

    private CommandBindResult? TryParse(
        Parameter parameter,
        string token,
        ICommandRoom? room,
        out object? value
    )
    {
        value = null;

        if (parameter.Parser is { } parser)
            return parser.TryParse(token, out value)
                ? null
                : CommandBindResult.Failure(
                    CommandReplyKeys.BAD_VALUE,
                    parameter.Limits.Sensitive ? "[redacted]" : token,
                    parameter.Name.ToLowerInvariant(),
                    Usage
                );

        switch (parameter.Kind)
        {
            case Kind.Word:
                value = token;

                return null;
            case Kind.Rest:
                value = new RestOfLine(token);

                return null;
            case Kind.RoomPlayer:
                if (room is null)
                    return CommandBindResult.Failure(CommandReplyKeys.NEEDS_ROOM);

                value = room.FindPlayer(token);

                return value is null
                    ? CommandBindResult.Failure(CommandReplyKeys.TARGET_NOT_FOUND, token)
                    : null;
            case Kind.Player:
                value = new PlayerTarget(token);

                return null;
            case Kind.Duration when CommandDuration.TryParse(token, out var duration):
                value = duration;

                return null;
            case Kind.Integer
                when int.TryParse(
                    token,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var integer
                ):
                value = integer;

                return null;
            case Kind.Long
                when long.TryParse(
                    token,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var number
                ):
                value = number;

                return null;
            case Kind.Boolean when TryParseBool(token, out var flag):
                value = flag;

                return null;
            case Kind.Enumeration
                when Enum.GetNames(parameter.ValueType)
                    .Contains(token, StringComparer.OrdinalIgnoreCase)
                    && Enum.TryParse(parameter.ValueType, token, ignoreCase: true, out var member)
                    && Enum.IsDefined(parameter.ValueType, member!):
                value = member;

                return null;
            default:
                return CommandBindResult.Failure(
                    CommandReplyKeys.BAD_VALUE,
                    parameter.Limits.Sensitive ? "[redacted]" : token,
                    parameter.Name.ToLowerInvariant(),
                    Usage
                );
        }
    }

    private static bool TryParseBool(string token, out bool value)
    {
        switch (token.ToLowerInvariant())
        {
            case "true" or "on" or "yes" or "1":
                value = true;

                return true;
            case "false" or "off" or "no" or "0":
                value = false;

                return true;
            default:
                value = false;

                return false;
        }
    }

    private static Parameter Describe(
        string commandName,
        ParameterInfo info,
        NullabilityInfoContext nullability,
        CommandArgumentParserRegistry? parsers
    )
    {
        var type = info.ParameterType;
        var underlying = Nullable.GetUnderlyingType(type);
        var isNullable =
            underlying is not null
            || (
                !type.IsValueType
                && nullability.Create(info).WriteState == NullabilityState.Nullable
            );
        var valueType = underlying ?? type;
        var optional = isNullable || info.HasDefaultValue;

        Kind kind;
        var parser = parsers?.Find(valueType);

        if (valueType == typeof(string))
            kind = Kind.Word;
        else if (valueType == typeof(int))
            kind = Kind.Integer;
        else if (valueType == typeof(long))
            kind = Kind.Long;
        else if (valueType == typeof(bool))
            kind = Kind.Boolean;
        else if (valueType.IsEnum)
            kind = Kind.Enumeration;
        else if (valueType == typeof(IRoomPlayer))
            kind = Kind.RoomPlayer;
        else if (valueType == typeof(PlayerTarget))
            kind = Kind.Player;
        else if (valueType == typeof(CommandDuration))
            kind = Kind.Duration;
        else if (valueType == typeof(RestOfLine))
            kind = Kind.Rest;
        else if (parser is not null)
            kind = parser.Kind;
        else
            throw new InvalidOperationException(
                $"Command '{commandName}': parameter '{info.Name}' has unsupported type {type.Name}."
            );

        var defaultValue = info.HasDefaultValue ? info.DefaultValue : null;

        if (defaultValue is DBNull)
            defaultValue = null;

        // A non-nullable value type that is merely optional needs its default boxed, not null.
        if (defaultValue is null && valueType.IsValueType && underlying is null)
            defaultValue = Activator.CreateInstance(valueType);

        var limits =
            info.GetCustomAttribute<CommandParameterAttribute>() ?? new CommandParameterAttribute();
        if (
            (limits.Minimum is not null || limits.Maximum is not null)
            && kind != Kind.Integer
            && kind != Kind.Long
        )
            throw new InvalidOperationException(
                $"Command '{commandName}': numeric limits require an integer parameter."
            );
        if (
            (
                limits.Minimum is not null
                && !long.TryParse(
                    limits.Minimum,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _
                )
            )
            || (
                limits.Maximum is not null
                && !long.TryParse(
                    limits.Maximum,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _
                )
            )
            || (
                limits.Minimum is not null
                && limits.Maximum is not null
                && long.Parse(limits.Minimum, CultureInfo.InvariantCulture)
                    > long.Parse(limits.Maximum, CultureInfo.InvariantCulture)
            )
            || limits.MinLength < -1
            || limits.MaxLength < -1
            || (limits.MaxLength >= 0 && limits.MinLength > limits.MaxLength)
        )
            throw new InvalidOperationException(
                $"Command '{commandName}': invalid limits on '{info.Name}'."
            );
        if (optional && defaultValue is not null)
        {
            var text = Convert.ToString(defaultValue, CultureInfo.InvariantCulture) ?? string.Empty;
            if (
                (limits.MinLength >= 0 && text.Length < limits.MinLength)
                || (limits.MaxLength >= 0 && text.Length > limits.MaxLength)
                || (
                    (kind == Kind.Integer || kind == Kind.Long)
                    && (
                        (
                            limits.Minimum is { } min
                            && Convert.ToInt64(defaultValue, CultureInfo.InvariantCulture)
                                < long.Parse(min, CultureInfo.InvariantCulture)
                        )
                        || (
                            limits.Maximum is { } max
                            && Convert.ToInt64(defaultValue, CultureInfo.InvariantCulture)
                                > long.Parse(max, CultureInfo.InvariantCulture)
                        )
                    )
                )
            )
                throw new InvalidOperationException(
                    $"Command '{commandName}': default for '{info.Name}' violates its constraints."
                );
        }
        return new Parameter(
            info.Name ?? string.Empty,
            kind,
            valueType,
            optional,
            defaultValue,
            info.GetCustomAttribute<SelectorsAttribute>()?.Node,
            info.GetCustomAttribute<SuggestAttribute>()?.Source,
            limits,
            parser
        );
    }

    private static Func<object?[], object> Compile(ConstructorInfo constructor)
    {
        var args = Expression.Parameter(typeof(object?[]), "args");
        var parameters = constructor.GetParameters();
        var arguments = new Expression[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
            arguments[i] = Expression.Convert(
                Expression.ArrayIndex(args, Expression.Constant(i)),
                parameters[i].ParameterType
            );

        return Expression
            .Lambda<Func<object?[], object>>(
                Expression.Convert(Expression.New(constructor, arguments), typeof(object)),
                args
            )
            .Compile();
    }

    private static string BuildUsage(string commandName, IReadOnlyList<Parameter> parameters)
    {
        var usage = new StringBuilder(":").Append(commandName);

        foreach (var parameter in parameters)
            usage
                .Append(' ')
                .Append(parameter.Optional ? '[' : '<')
                .Append(parameter.Name.ToLowerInvariant())
                .Append(parameter.Optional ? ']' : '>');

        return usage.ToString();
    }
}
