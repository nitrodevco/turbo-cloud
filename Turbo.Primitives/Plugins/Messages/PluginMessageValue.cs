using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Plugins.Messages.Enums;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// One value of a plugin message's schema, worked out once from its CLR type: how to write it, read
/// it and describe it to clients. Built by <see cref="PluginMessageSchema"/>.
/// </summary>
internal sealed class PluginMessageValue
{
    private static readonly MethodInfo ARRAY_HELPERS = typeof(PluginMessageValue).GetMethod(
        nameof(ArrayHelpers),
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private PluginMessageValue(PluginMessageValueKind kind, Type type)
    {
        Kind = kind;
        Type = type;
    }

    public PluginMessageValueKind Kind { get; }

    public Type Type { get; }

    /// <summary>An array's item.</summary>
    public PluginMessageValue? Item { get; private init; }

    /// <summary>A record's fields, in constructor order.</summary>
    public ImmutableArray<PluginMessageField> Fields { get; private init; } = [];

    private ConstructorInfo? Constructor { get; init; }

    // An array's items as a list to write (null and a default ImmutableArray as none), and the
    // items read back as the field's own collection type.
    private Func<object?, IList>? Items { get; init; }

    private Func<Array, object>? FromItems { get; init; }

    public static PluginMessageValue For(Type type, string path, Stack<Type> building)
    {
        if (type == typeof(int))
            return new(PluginMessageValueKind.Int, type);

        if (type == typeof(long))
            return new(PluginMessageValueKind.Long, type);

        if (type == typeof(bool))
            return new(PluginMessageValueKind.Bool, type);

        if (type == typeof(string))
            return new(PluginMessageValueKind.String, type);

        if (type == typeof(double))
            return new(PluginMessageValueKind.Double, type);

        if (type.IsEnum)
        {
            var underlying = Enum.GetUnderlyingType(type);

            if (
                underlying != typeof(int)
                && underlying != typeof(short)
                && underlying != typeof(ushort)
                && underlying != typeof(byte)
                && underlying != typeof(sbyte)
            )
                throw Unsupported(
                    path,
                    type,
                    "an enum is sent as an int, and its values do not fit one"
                );

            return new(PluginMessageValueKind.Int, type);
        }

        if (ItemTypeOf(type) is { } itemType)
        {
            var item = For(itemType, path + "[]", building);
            var (items, fromItems) = ((Func<object?, IList>, Func<Array, object>))
                ARRAY_HELPERS.MakeGenericMethod(itemType).Invoke(null, [type])!;

            return new(PluginMessageValueKind.Array, type)
            {
                Item = item,
                Items = items,
                FromItems = fromItems,
            };
        }

        if (
            type.IsClass
            && !type.IsAbstract
            && type != typeof(object)
            && !typeof(IEnumerable).IsAssignableFrom(type)
            && !typeof(Delegate).IsAssignableFrom(type)
        )
        {
            if (building.Contains(type))
                throw Unsupported(path, type, "a record cannot contain itself");

            building.Push(type);

            try
            {
                var (constructor, fields) = FieldsOf(type, path, building);

                return new(PluginMessageValueKind.Object, type)
                {
                    Constructor = constructor,
                    Fields = fields,
                };
            }
            finally
            {
                building.Pop();
            }
        }

        throw Unsupported(path, type, null);
    }

    /// <summary>
    /// The record's fields: the parameters of its one public constructor (the primary one; a
    /// record's copy constructor is protected), each read back from the property of its name.
    /// </summary>
    public static (ConstructorInfo Constructor, ImmutableArray<PluginMessageField> Fields) FieldsOf(
        Type type,
        string path,
        Stack<Type> building
    )
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetParameters() is not [{ } only] || only.ParameterType != type)
            .ToArray();

        if (constructors.Length != 1)
            throw new ArgumentException(
                $"{path}: {type.Name} needs exactly one public constructor taking its fields in order (a positional record), not {constructors.Length}."
            );

        var constructor = constructors[0];
        var fields = ImmutableArray.CreateBuilder<PluginMessageField>();

        foreach (var parameter in constructor.GetParameters())
        {
            var name = parameter.Name!;
            var property =
                type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException(
                    $"{path}.{name}: {type.Name} has no public property {name} to read the field from."
                );

            fields.Add(
                new PluginMessageField(
                    char.ToLowerInvariant(name[0]) + name[1..],
                    property,
                    For(parameter.ParameterType, $"{path}.{name}", building)
                )
            );
        }

        return (constructor, fields.ToImmutable());
    }

    public void Write(IServerPacket packet, object? value)
    {
        switch (Kind)
        {
            case PluginMessageValueKind.Int:
                packet.WriteInteger(value is null ? 0 : Convert.ToInt32(value));
                break;
            case PluginMessageValueKind.Long:
                packet.WriteLong(value is long l ? l : 0);
                break;
            case PluginMessageValueKind.Bool:
                packet.WriteBoolean(value is true);
                break;
            case PluginMessageValueKind.String:
                packet.WriteString(value as string ?? string.Empty);
                break;
            case PluginMessageValueKind.Double:
                packet.WriteDouble(value is double d ? d : 0);
                break;
            case PluginMessageValueKind.Array:
                var items = Items!(value);

                packet.WriteInteger(items.Count);

                foreach (var item in items)
                    Item!.Write(packet, item);

                break;
            case PluginMessageValueKind.Object:
                // A null record is sent as its defaults, as a client fills in a missing one.
                foreach (var field in Fields)
                    field.Value.Write(
                        packet,
                        value is null ? null : field.Property.GetValue(value)
                    );

                break;
        }
    }

    public object? Read(IClientPacket packet)
    {
        switch (Kind)
        {
            case PluginMessageValueKind.Int:
                var number = packet.PopInt();

                return Type.IsEnum ? Enum.ToObject(Type, number) : number;
            case PluginMessageValueKind.Long:
                return packet.PopLong();
            case PluginMessageValueKind.Bool:
                return packet.PopBoolean();
            case PluginMessageValueKind.String:
                return packet.PopString();
            case PluginMessageValueKind.Double:
                return BitConverter.Int64BitsToDouble(packet.PopLong());
            case PluginMessageValueKind.Array:
                var count = packet.PopInt();

                // An item takes at least a byte unless it is an empty record, so a count past what is
                // left is a hostile one, not a big array.
                if (
                    count < 0
                    || count > Math.Max(packet.Remaining, Item!.Fields.IsEmpty ? 1024 : 0)
                )
                    throw new InvalidDataException(
                        $"An array of {count} items with {packet.Remaining} bytes left."
                    );

                var items = Array.CreateInstance(Item.Type, count);

                for (var i = 0; i < count; i++)
                    items.SetValue(Item.Read(packet), i);

                return FromItems!(items);
            default:
                var args = new object?[Fields.Length];

                for (var i = 0; i < Fields.Length; i++)
                    args[i] = Fields[i].Value.Read(packet);

                return Constructor!.Invoke(args);
        }
    }

    public void WriteJson(Utf8JsonWriter json)
    {
        switch (Kind)
        {
            case PluginMessageValueKind.Array:
                json.WriteStartArray();
                json.WriteStringValue("array");
                Item!.WriteJson(json);
                json.WriteEndArray();
                break;
            case PluginMessageValueKind.Object:
                json.WriteStartArray();
                json.WriteStringValue("object");
                WriteFieldsJson(json, Fields);
                json.WriteEndArray();
                break;
            default:
                json.WriteStringValue(
                    Kind switch
                    {
                        PluginMessageValueKind.Int => "int",
                        PluginMessageValueKind.Long => "long",
                        PluginMessageValueKind.Bool => "bool",
                        PluginMessageValueKind.Double => "double",
                        _ => "string",
                    }
                );
                break;
        }
    }

    public static void WriteFieldsJson(
        Utf8JsonWriter json,
        ImmutableArray<PluginMessageField> fields
    )
    {
        json.WriteStartArray();

        foreach (var field in fields)
        {
            json.WriteStartArray();
            json.WriteStringValue(field.Name);
            field.Value.WriteJson(json);
            json.WriteEndArray();
        }

        json.WriteEndArray();
    }

    private static Type? ItemTypeOf(Type type)
    {
        if (type.IsArray)
            return type.GetArrayRank() == 1 ? type.GetElementType() : null;

        if (!type.IsGenericType)
            return null;

        var definition = type.GetGenericTypeDefinition();

        return definition == typeof(ImmutableArray<>) || definition == typeof(IReadOnlyList<>)
            ? type.GetGenericArguments()[0]
            : null;
    }

    private static (Func<object?, IList>, Func<Array, object>) ArrayHelpers<T>(Type type)
    {
        Func<object?, IList> items = value =>
            value switch
            {
                ImmutableArray<T> { IsDefault: true } => Array.Empty<T>(),
                ImmutableArray<T> array => array,
                IList list => list,
                IEnumerable<T> other => other.ToArray(),
                _ => Array.Empty<T>(),
            };
        Func<Array, object> fromItems =
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)
                ? array => ImmutableArray.Create((T[])array)
                : array => array;

        return (items, fromItems);
    }

    private static ArgumentException Unsupported(string path, Type type, string? why) =>
        new(
            $"{path}: {type.Name} is not a type a plugin message can carry"
                + (why is null ? "" : $" ({why})")
                + ". Supported: int, long, bool, string, double, an enum, a record of those, or an array (T[], ImmutableArray<T>, IReadOnlyList<T>) of any of them."
        );
}
