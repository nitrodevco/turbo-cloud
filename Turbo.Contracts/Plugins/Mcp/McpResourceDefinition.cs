using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// A live resource as an MCP server offers it: its address (a URI, or a URI template whose values
/// are read into its arguments' type), words, MIME type, the nodes that may read it (any one) and
/// the plugin that brought it. The host's registry (<see cref="IMcpResources"/>) and the test kit
/// both read resources through it.
/// </summary>
public sealed partial class McpResourceDefinition
{
    private readonly JsonSerializerOptions _json;
    private readonly JsonObject? _arguments;
    private readonly Regex? _match;

    private McpResourceDefinition(
        IMcpResource resource,
        string owner,
        ImmutableArray<string> nodes,
        JsonSerializerOptions json,
        JsonObject? arguments,
        Regex? match
    )
    {
        Resource = resource;
        Owner = owner;
        Nodes = nodes;
        _json = json;
        _arguments = arguments;
        _match = match;
    }

    /// <summary>The plugin that brought it (its key).</summary>
    public string Owner { get; }

    public IMcpResource Resource { get; }

    /// <summary>Its URI, or its URI template (<see cref="IsTemplate"/>).</summary>
    public string Uri => Resource.Uri;

    public string Name => Resource.Name;

    public string Title => Resource.Title;

    public string Description => Resource.Description;

    public string MimeType => Resource.MimeType;

    /// <summary>Its address is a URI template (<c>resources/templates/list</c>), not one URI (<c>resources/list</c>).</summary>
    public bool IsTemplate => _match is not null;

    /// <summary>The nodes of its <c>[RequiresPermission]</c>: a caller holding any one may read it.</summary>
    public ImmutableArray<string> Nodes { get; }

    /// <summary>
    /// The resource as an MCP server offers it. Throws <see cref="InvalidOperationException"/>, in
    /// words for its author, for an address that is not a URI or a template whose values are not
    /// its arguments' properties, a bad name, no description or no <c>[RequiresPermission]</c>.
    /// </summary>
    public static McpResourceDefinition Create(IMcpResource resource, string owner)
    {
        var name = McpContribution.CheckName(resource.Name, "resource", resource.GetType().Name);
        var what = $"The MCP resource {name}";

        McpContribution.CheckDescription(resource.Description, what, "what it holds");

        var nodes = McpContribution.Nodes(resource.GetType(), what);
        var uri = resource.Uri ?? string.Empty;
        var json = McpToolDefinition.Options();

        if (!UriPattern().IsMatch(uri) || uri.Any(char.IsWhiteSpace))
            throw new InvalidOperationException(
                $"{what} has the address '{uri}': an address is a URI with a scheme, such as turbo://rooms/{{roomId}}."
            );

        if (string.IsNullOrWhiteSpace(resource.MimeType))
            throw new InvalidOperationException(
                $"{what} has no MIME type: say what its contents are, such as application/json."
            );

        var names = TemplateNames(uri, what);

        if (resource.ArgumentsType is not { } type)
        {
            if (names.Count > 0)
                throw new InvalidOperationException(
                    $"{what}'s address {uri} has values ({string.Join(", ", names)}): derive it from McpResourceTemplate<TArgs>, whose TArgs has them as properties."
                );

            return new McpResourceDefinition(resource, owner, nodes, json, null, null);
        }

        if (names.Count == 0)
            throw new InvalidOperationException(
                $"{what} is a template with no values in its address {uri}: put each in braces, such as turbo://rooms/{{roomId}}, or derive it from McpResource."
            );

        var schema = McpToolDefinition.Schema(json, type, what, "arguments");
        var properties = schema["properties"] as JsonObject ?? [];

        foreach (var value in names)
            if (!properties.ContainsKey(value))
                throw new InvalidOperationException(
                    $"{what}'s address has {{{value}}}, which its arguments type {type.Name} has no property for. Its properties are {string.Join(", ", properties.Select(x => x.Key))}."
                );

        foreach (var property in properties)
            if (!names.Contains(property.Key))
                throw new InvalidOperationException(
                    $"{what}'s arguments type {type.Name} has {property.Key}, which its address {uri} has no {{{property.Key}}} for."
                );

        McpContribution.CheckScalars(properties, what);

        return new McpResourceDefinition(resource, owner, nodes, json, schema, Matcher(uri));
    }

    /// <summary>Whether <paramref name="caller"/> may read it: the staff member holds one of its <see cref="Nodes"/>.</summary>
    public Task<bool> IsAvailableToAsync(
        McpCaller caller,
        Func<string, CancellationToken, Task<bool>> holds,
        CancellationToken ct
    ) => McpContribution.HoldsAnyAsync(Nodes, holds, ct);

    /// <summary>Whether <paramref name="uri"/> is this resource's: its URI, or one its template makes.</summary>
    public bool Matches(string uri) =>
        _match is null ? string.Equals(uri, Uri, StringComparison.Ordinal) : _match.IsMatch(uri);

    /// <summary>
    /// Reads the resource at <paramref name="uri"/> (one <see cref="Matches"/>): the template's
    /// values read into its arguments, then the resource. A value that does not fit its property
    /// answers <see cref="McpResourceReadStatus.NotFound"/>. What the resource throws is thrown.
    /// </summary>
    public async Task<McpResourceReadResult> ReadAsync(
        string uri,
        McpToolContext ctx,
        CancellationToken ct
    )
    {
        object? arguments = null;

        if (_match is not null)
        {
            var found = _match.Match(uri);

            if (!found.Success)
                return McpResourceReadResult.NotFound(uri);

            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var group in _match.GetGroupNames().Where(x => !int.TryParse(x, out _)))
                values[group] = System.Uri.UnescapeDataString(found.Groups[group].Value);

            var problems = new List<string>();
            var args = McpContribution.FromText(values, _arguments!, problems);

            if (problems.Count > 0)
                return McpResourceReadResult.NotFound(uri, string.Join(" ", problems));

            arguments = args.Deserialize(Resource.ArgumentsType!, _json);
        }

        var result = await Resource.InvokeAsync(arguments, ctx, ct).ConfigureAwait(false);

        if (result.Missing is { } missing)
            return McpResourceReadResult.NotFound(uri, missing);

        string text;

        if (result.Value is { } value)
        {
            var element = JsonSerializer.SerializeToElement(value, value.GetType(), _json);

            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException(
                    $"The MCP resource {Name} answered {element.ValueKind} data: its data is an object; put a list in a property of one."
                );

            text = element.GetRawText();
        }
        else
            text = result.Text ?? string.Empty;

        return new McpResourceReadResult(
            McpResourceReadStatus.Ok,
            [new McpResourceContents(uri, MimeType, text)],
            text.Contains($"\"{PlayerText.PROPERTY}\"", StringComparison.Ordinal)
                ? PlayerText.NOTE
                : null
        );
    }

    /// <summary>The <c>{name}</c> values of a URI template, in order.</summary>
    private static List<string> TemplateNames(string uri, string what)
    {
        var names = new List<string>();

        foreach (Match value in TemplateValue().Matches(uri))
        {
            var name = value.Groups[1].Value;

            if (name.Length == 0 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                throw new InvalidOperationException(
                    $"{what}'s address has {{{name}}}: a value's name is letters, digits and '_', such as {{roomId}}."
                );

            if (names.Contains(name))
                throw new InvalidOperationException($"{what}'s address has {{{name}}} twice.");

            names.Add(name);
        }

        return names;
    }

    /// <summary>The template as a pattern: each value one or more characters that are not <c>/</c>, <c>?</c> or <c>#</c>.</summary>
    private static Regex Matcher(string template)
    {
        var pattern = new StringBuilder("^");
        var at = 0;

        foreach (Match value in TemplateValue().Matches(template))
        {
            pattern.Append(Regex.Escape(template[at..value.Index]));
            pattern.Append($"(?<{value.Groups[1].Value}>[^/?#]+)");
            at = value.Index + value.Length;
        }

        pattern.Append(Regex.Escape(template[at..])).Append('$');

        return new Regex(
            pattern.ToString(),
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)
        );
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:.+")]
    private static partial Regex UriPattern();

    [GeneratedRegex(@"\{([^}]*)\}")]
    private static partial Regex TemplateValue();
}

/// <summary>How reading a resource went.</summary>
public enum McpResourceReadStatus
{
    /// <summary>It was read.</summary>
    Ok,

    /// <summary>
    /// No resource the caller may read is at the address: none matches it, the caller holds none
    /// of its nodes, or the resource says nothing is there. MCP's "resource not found".
    /// </summary>
    NotFound,

    /// <summary>It threw; the error is in the server's log, not in the answer.</summary>
    Failed,
}

/// <summary>One resource's contents, as <c>resources/read</c> sends them.</summary>
public sealed record McpResourceContents(string Uri, string MimeType, string Text);

/// <summary>
/// What reading a resource answered: its contents, or why it was not found; <see cref="Note"/>
/// is <see cref="PlayerText.NOTE"/> when the contents hold player text.
/// </summary>
public sealed record McpResourceReadResult(
    McpResourceReadStatus Status,
    ImmutableArray<McpResourceContents> Contents,
    string? Note = null,
    string? Message = null
)
{
    public static McpResourceReadResult NotFound(string uri, string? why = null) =>
        new(
            McpResourceReadStatus.NotFound,
            [],
            Message: why is null
                ? $"There is no resource {uri} you may read. resources/list and resources/templates/list name the ones you may."
                : $"There is nothing at {uri}: {why}"
        );

    public static McpResourceReadResult Failed(string uri) =>
        new(
            McpResourceReadStatus.Failed,
            [],
            Message: $"Reading {uri} failed on the server. Its error is in the server's log; tell the hotel's staff."
        );
}
