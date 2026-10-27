using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Plugins.Messages.Enums;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// A plugin message's name and fields, read once from its record, and the generic codec they
/// give: every field in Habbo's wire encoding, in constructor order (<c>docs/plugin-messages.md</c>).
/// </summary>
public sealed class PluginMessageSchema
{
    // Weak on the type, so a plugin's schemas go with its unloaded assembly.
    private static readonly ConditionalWeakTable<Type, PluginMessageSchema> CACHE = [];

    private readonly PluginMessageValue _value;

    private PluginMessageSchema(string name, Type type, PluginMessageValue value)
    {
        Name = name;
        Type = type;
        _value = value;
        Json = ToJson(value);
        CarriesPacket = value.Fields is [.., { Value.Kind: PluginMessageValueKind.Packet }];
    }

    /// <summary>The name its <see cref="PluginMessageAttribute"/> gives.</summary>
    public string Name { get; }

    public Type Type { get; }

    /// <summary>
    /// The fields as clients are sent them: a JSON array of <c>[name, type]</c>, where a type is
    /// <c>"int"</c>, <c>"long"</c>, <c>"bool"</c>, <c>"string"</c>, <c>"double"</c>,
    /// <c>["array", type]</c>, <c>["map", type]</c> (string keys), <c>["object", fields]</c> or, as
    /// the last field only, <c>"packet"</c> (a carried packet). Names are camelCase.
    /// </summary>
    public string Json { get; }

    /// <summary>
    /// Its last field is an <see cref="IComposer"/>: a whole packet of the hotel's carried after the
    /// other fields, sent as a <see cref="PluginEnvelopeComposer"/>. Such a message goes only from
    /// the server to clients.
    /// </summary>
    public bool CarriesPacket { get; }

    /// <summary>The schema of <paramref name="type"/>, worked out on first use.</summary>
    /// <exception cref="ArgumentException">
    /// The type is not an <see cref="IPluginMessage"/> with a <see cref="PluginMessageAttribute"/>,
    /// or a field is of a type the codec cannot carry (the message names the field).
    /// </exception>
    public static PluginMessageSchema For(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (CACHE.TryGetValue(type, out var cached))
            return cached;

        var schema = Build(type);

        return CACHE.GetValue(type, _ => schema);
    }

    /// <summary>
    /// Writes <paramref name="message"/>'s fields, without a header or length; a carried packet is
    /// not among them (<see cref="CarriedOf"/>).
    /// </summary>
    public void Write(IServerPacket packet, IPluginMessage message)
    {
        CheckType(message);
        _value.Write(packet, message);
    }

    /// <summary>The packet <paramref name="message"/> carries, or null for none (or no such field).</summary>
    public IComposer? CarriedOf(IPluginMessage message)
    {
        CheckType(message);

        return CarriesPacket ? _value.Fields[^1].Property.GetValue(message) as IComposer : null;
    }

    private void CheckType(IPluginMessage message)
    {
        if (message.GetType() != Type)
            throw new ArgumentException(
                $"{message.GetType().Name} is not {Type.Name}, the message {Name}.",
                nameof(message)
            );
    }

    /// <summary>Reads a message's fields from <paramref name="packet"/>.</summary>
    /// <exception cref="InvalidDataException">An array count is past what the packet holds.</exception>
    public IPluginMessage Read(IClientPacket packet)
    {
        try
        {
            return (IPluginMessage)_value.Read(packet)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // The record's own constructor refused a value: that, not the reflection around it.
            throw ex.InnerException;
        }
    }

    private static PluginMessageSchema Build(Type type)
    {
        if (!typeof(IPluginMessage).IsAssignableFrom(type))
            throw new ArgumentException(
                $"{type.Name} is not an {nameof(IPluginMessage)}.",
                nameof(type)
            );

        if (
            type.GetCustomAttribute<PluginMessageAttribute>() is not { } attribute
            || string.IsNullOrWhiteSpace(attribute.Name)
        )
            throw new ArgumentException(
                $"{type.Name} has no [{nameof(PluginMessageAttribute)[..^9]}(\"<plugin>.<name>\")].",
                nameof(type)
            );

        if (!type.IsClass || type.IsAbstract)
            throw new ArgumentException(
                $"{attribute.Name}: {type.Name} must be a sealed record.",
                nameof(type)
            );

        var value = PluginMessageValue.For(type, attribute.Name, new Stack<Type>());

        // A carried packet is the rest of the packet, so nothing can follow it or hold it.
        for (var i = 0; i < value.Fields.Length; i++)
        {
            var field = value.Fields[i];
            var last = i == value.Fields.Length - 1;

            if (
                (field.Value.Kind == PluginMessageValueKind.Packet && !last)
                || (field.Value.Kind != PluginMessageValueKind.Packet && field.Value.HasPacket())
            )
                throw new ArgumentException(
                    $"{attribute.Name}.{field.Property.Name}: a carried packet (IComposer) is only the message's last field, never inside an array, a dictionary or a record.",
                    nameof(type)
                );
        }

        return new PluginMessageSchema(attribute.Name, type, value);
    }

    private static string ToJson(PluginMessageValue value)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
            PluginMessageValue.WriteFieldsJson(json, value.Fields);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
