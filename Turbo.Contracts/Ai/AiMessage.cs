using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Turbo.Contracts.Ai;

/// <summary>Who said a message: the plugin's side (prompts and tool results) or the model.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AiRole>))]
public enum AiRole
{
    User,
    Assistant,
}

/// <summary>
/// One message of a conversation, the same for every provider (each driver writes it in its own
/// shape): text, the model's tool calls, and the results sent back for them.
/// </summary>
public sealed record AiMessage(AiRole Role, ImmutableArray<AiPart> Parts)
{
    /// <summary>Its text parts, joined.</summary>
    [JsonIgnore]
    public string Text => string.Concat(Parts.OfType<AiTextPart>().Select(x => x.Text));

    public static AiMessage User(string text) => new(AiRole.User, [new AiTextPart(text)]);

    public static AiMessage Assistant(string text) => new(AiRole.Assistant, [new AiTextPart(text)]);
}

/// <summary>A piece of a message.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AiTextPart), "text")]
[JsonDerivedType(typeof(AiToolCallPart), "tool_call")]
[JsonDerivedType(typeof(AiToolResultPart), "tool_result")]
public abstract record AiPart;

/// <summary>Text.</summary>
public sealed record AiTextPart(string Text) : AiPart;

/// <summary>The model calls a tool: its call id (the provider's), the tool's name and the arguments.</summary>
public sealed record AiToolCallPart(string Id, string Name, JsonElement Arguments) : AiPart;

/// <summary>What a tool answered, for the call <see cref="CallId"/>; an error the model reads and acts on.</summary>
public sealed record AiToolResultPart(string CallId, string Name, string Text, bool IsError = false)
    : AiPart;
