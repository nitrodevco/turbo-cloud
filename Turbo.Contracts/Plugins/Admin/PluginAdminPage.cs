using System.Collections.Generic;

namespace Turbo.Contracts.Plugins.Admin;

/// <summary>
/// A page a plugin adds to the panel, as data: shown to staff holding <see cref="Node"/>, at
/// <c>/plugin/&lt;key&gt;/&lt;id&gt;</c> (and <c>/&lt;id&gt;/&lt;item&gt;</c> for a detail page, its
/// endpoints' <c>{id}</c>). Endpoints are relative to the plugin's own (<c>reports/{id}</c>).
/// </summary>
public sealed record PluginAdminPage(
    string Id,
    string Title,
    string Node,
    IReadOnlyList<PluginAdminBlock> Blocks,
    string? Description = null,
    bool Nav = true,
    string? Icon = null
);

/// <summary>
/// One block of a page. <see cref="Type"/>: <c>text</c> (<see cref="Text"/>, paragraphs and
/// <c>**bold**</c>), <c>stats</c> (tiles: the endpoint answers <c>[{label, value}]</c>),
/// <c>table</c> (the endpoint answers <c>{items, total, pageSize}</c> for <c>?page=&amp;&lt;filter&gt;=</c>;
/// a row opens <see cref="ItemPage"/> with its <c>id</c>), <c>detail</c> (one object from the
/// endpoint, its <see cref="Fields"/>, an <see cref="Image"/> and <see cref="Downloads"/>),
/// <c>form</c> (<see cref="Fields"/> sent as JSON to <see cref="Submit"/>) and <c>actions</c>
/// (buttons). The panel shows a block of a type it does not know as unsupported.
/// </summary>
public sealed record PluginAdminBlock(
    string Type,
    string? Title = null,
    string? Text = null,
    string? Endpoint = null,
    IReadOnlyList<PluginAdminColumn>? Columns = null,
    IReadOnlyList<PluginAdminField>? Filters = null,
    IReadOnlyList<PluginAdminField>? Fields = null,
    IReadOnlyList<PluginAdminAction>? Actions = null,
    string? ItemPage = null,
    string? Image = null,
    IReadOnlyList<PluginAdminAction>? Downloads = null,
    PluginAdminAction? Submit = null
);

/// <summary>A table column or a detail field shown: <see cref="Type"/> <c>text</c>, <c>number</c>, <c>time</c>, <c>bool</c> or <c>badge</c>.</summary>
public sealed record PluginAdminColumn(string Key, string Label, string Type = "text");

/// <summary>
/// A form field or a table filter: <see cref="Type"/> <c>text</c>, <c>textarea</c>, <c>number</c>,
/// <c>bool</c> or <c>select</c> (<see cref="Options"/>), checked in the panel and again by the plugin.
/// </summary>
public sealed record PluginAdminField(
    string Key,
    string Label,
    string Type = "text",
    bool Required = false,
    double? Min = null,
    double? Max = null,
    string? Pattern = null,
    IReadOnlyList<string>? Options = null,
    string? Hint = null
);

/// <summary>
/// A button: <see cref="Method"/> <see cref="Endpoint"/> after <see cref="Confirm"/> (when set),
/// then <see cref="Then"/>: <c>refresh</c> or <c>back</c> (to the page before). Shown to holders
/// of <see cref="Node"/> when set; <see cref="When"/> <c>key=value</c> shows it only for an item
/// whose field has that value.
/// </summary>
public sealed record PluginAdminAction(
    string Label,
    string Method,
    string Endpoint,
    string? Confirm = null,
    bool Danger = false,
    string? Node = null,
    string Then = "refresh",
    string? When = null
);
