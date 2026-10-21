using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using static Turbo.Assets.XmlAttributes;

namespace Turbo.Assets.Conversion;

/// <summary>
/// The avatar tables nitro-renderer starts from, out of the XML assets of Habbo's
/// <c>habbo-avatar-render-lib</c> (what Flash's <c>AvatarRenderManager</c> reads before anything is
/// downloaded), in the shapes nitro-studio's <c>server/avatar-tables.ts</c> and
/// <c>habbo-client-data.ts</c> give them: <c>HabboAvatarGeometry</c>, <c>HabboAvatarPartSets</c>,
/// a built-in animation (<c>dance_sixseven_animation</c>), an action's offsets
/// (<c>action_offset_lay</c>) and <c>HabboAvatarAnimation</c>. A number reads as JavaScript's
/// <c>Number</c> reads an attribute, so a missing one is <c>null</c>, as the Studio writes NaN.
/// </summary>
public static class AvatarTablesMapper
{
    public static JsonObject Geometry(string xml)
    {
        var root = Root(xml, "geometry");
        var camera = root.Element("camera");

        return new JsonObject
        {
            ["direction"] = Num(root, "direction"),
            ["camera"] = new JsonObject
            {
                ["x"] = Text(camera, "x"),
                ["y"] = Text(camera, "y"),
                ["z"] = Text(camera, "z"),
            },
            ["canvases"] = Array(
                root.Elements("canvas"),
                canvas => new JsonObject
                {
                    ["scale"] = Str(canvas, "scale"),
                    ["geometries"] = Array(
                        canvas.Elements("geometry"),
                        geometry => new JsonObject
                        {
                            ["id"] = Str(geometry, "id"),
                            ["width"] = Num(geometry, "width"),
                            ["height"] = Num(geometry, "height"),
                            ["dx"] = Num(geometry, "dx"),
                            ["dy"] = Num(geometry, "dy"),
                        }
                    ),
                }
            ),
            ["avatarSets"] = Array(root.Elements("avatarset"), AvatarSet),
            ["types"] = Array(
                root.Elements("type"),
                type => new JsonObject
                {
                    ["id"] = Str(type, "id"),
                    ["bodyParts"] = Array(type.Elements("bodypart"), BodyPart),
                }
            ),
        };
    }

    public static JsonObject PartSets(string xml)
    {
        var root = Root(xml, "partSets");

        return new JsonObject
        {
            ["partSet"] = Array(
                root.Elements("partSet").Take(1).Elements("part"),
                part =>
                {
                    var output = new JsonObject { ["setType"] = Str(part, "set-type") };

                    Optional(output, "swim", part, "swim");
                    Optional(output, "flippedSetType", part, "flipped-set-type");
                    Optional(output, "removeSetType", part, "remove-set-type");

                    return output;
                }
            ),
            ["activePartSets"] = Array(
                root.Elements("activePartSet"),
                set => new JsonObject
                {
                    ["id"] = Str(set, "id"),
                    ["activeParts"] = Array(
                        set.Elements("activePart"),
                        part => new JsonObject { ["setType"] = Str(part, "set-type") }
                    ),
                }
            ),
        };
    }

    public static JsonObject BuiltInAnimation(string xml)
    {
        var root = Root(xml, "animation");

        return new JsonObject
        {
            ["name"] = Str(root, "name"),
            ["desc"] = Str(root, "desc"),
            ["frames"] = Array(
                root.Elements("frame"),
                frame => new JsonObject
                {
                    ["bodyparts"] = Array(
                        frame.Elements("bodypart"),
                        part => new JsonObject
                        {
                            ["id"] = Str(part, "id"),
                            ["action"] = Str(part, "action"),
                            ["frame"] = Num(part, "frame"),
                            ["dx"] = Num(part, "dx"),
                            ["dy"] = Num(part, "dy"),
                            ["dd"] = Num(part, "dd"),
                        }
                    ),
                }
            ),
        };
    }

    public static JsonObject ActionOffsets(string action, string xml) =>
        new()
        {
            ["action"] = action,
            ["offsets"] = Array(
                Root(xml, "offsets").Elements("offset"),
                offset => new JsonObject
                {
                    ["size"] = Str(offset, "size"),
                    ["direction"] = Num(offset, "direction"),
                    ["x"] = Num(offset, "x"),
                    ["y"] = Num(offset, "y"),
                    ["z"] = Num(offset, "z"),
                }
            ),
        };

    /// <summary><c>HabboAvatarAnimation</c> as nitro-renderer's <c>IAssetAvatarAnimation[]</c>.</summary>
    public static JsonArray Animations(string xml) =>
        Array(
            Root(xml, null).Descendants("action"),
            action =>
            {
                var output = new JsonObject
                {
                    ["id"] = Str(action, "id"),
                    ["parts"] = Array(
                        action.Descendants("part"),
                        part => new JsonObject
                        {
                            ["setType"] = Str(part, "set-type"),
                            ["frames"] = Array(
                                part.Descendants("frame"),
                                frame =>
                                {
                                    var mapped = new JsonObject
                                    {
                                        ["number"] = Num(frame, "number"),
                                        ["assetPartDefinition"] = Str(frame, "assetpartdefinition"),
                                    };

                                    Repeats(mapped, frame);

                                    return mapped;
                                }
                            ),
                        }
                    ),
                };

                if (action.Descendants("offsets").FirstOrDefault() is { } offsets)
                    output["offsets"] = new JsonObject
                    {
                        ["frames"] = Array(
                            offsets.Elements("frame"),
                            frame =>
                            {
                                var mapped = new JsonObject { ["id"] = Num(frame, "id") };

                                Repeats(mapped, frame);
                                mapped["directions"] = Array(
                                    frame.Descendants("direction"),
                                    direction => new JsonObject
                                    {
                                        ["id"] = Num(direction, "id"),
                                        ["bodyParts"] = Array(
                                            direction.Descendants("bodypart"),
                                            part => new JsonObject
                                            {
                                                ["id"] = Str(part, "id"),
                                                ["dx"] = Num(part, "dx"),
                                                ["dy"] = Num(part, "dy"),
                                            }
                                        ),
                                    }
                                );

                                return mapped;
                            }
                        ),
                    };

                return output;
            }
        );

    /// <summary>
    /// Habbo's <c>HabboAvatarActions.xml</c> (the gordon folder's, which the client downloads) as
    /// nitro-renderer's <c>IAssetAvatarActionData</c>, as the Studio's <c>HabboAvatarActionsMapper</c>
    /// maps it: each action with only the attributes it has.
    /// </summary>
    public static JsonObject Actions(string xml)
    {
        var root = Root(xml, "actions");
        var output = new JsonObject();
        var actions = root.Elements("action").ToList();

        if (actions.Count == 0)
            return output;

        output["actions"] = Array(
            actions,
            action =>
            {
                var mapped = new JsonObject();

                if (!action.HasAttributes)
                    return mapped;

                Optional(mapped, "id", action, "id");
                Optional(mapped, "state", action, "state");

                if (Attr(action, "precedence") is { } precedence)
                    mapped["precedence"] = JsValues.Int(precedence);

                Flag(mapped, "main", action, "main", "1");
                Flag(mapped, "isDefault", action, "isdefault", "1");
                Flag(mapped, "animation", action, "animation", "1");
                Flag(mapped, "startFromFrameZero", action, "startfromframezero", "true");
                Flag(mapped, "preventHeadTurn", action, "preventheadturn", "true");
                Optional(mapped, "geometryType", action, "geometrytype");
                Optional(mapped, "activePartSet", action, "activepartset");
                Optional(mapped, "assetPartDefinition", action, "assetpartdefinition");
                Optional(mapped, "lay", action, "lay");
                Prevents(mapped, action);

                if (action.Elements("type").Any())
                    mapped["types"] = Array(
                        action.Elements("type"),
                        type =>
                        {
                            var mappedType = new JsonObject();

                            if (Attr(type, "id") is { } id)
                                mappedType["id"] = JsValues.Int(id);

                            Flag(mappedType, "animated", type, "animated", "true");
                            Flag(mappedType, "preventHeadTurn", type, "preventheadturn", "true");
                            Prevents(mappedType, type);

                            return mappedType;
                        }
                    );

                if (action.Elements("param").Any())
                    mapped["params"] = Array(
                        action.Elements("param"),
                        param =>
                        {
                            var mappedParam = new JsonObject();

                            Optional(mappedParam, "id", param, "id");
                            Optional(mappedParam, "value", param, "value");

                            return mappedParam;
                        }
                    );

                return mapped;
            }
        );

        return output;

        static void Flag(JsonObject output, string key, XElement element, string name, string on)
        {
            if (Attr(element, name) is { } value)
                output[key] = value == on;
        }

        static void Prevents(JsonObject output, XElement element)
        {
            if (Attr(element, "prevents") is { Length: > 0 } prevents)
                output["prevents"] = new JsonArray([
                    .. prevents.Split(',').Select(x => (JsonNode)x),
                ]);
        }
    }

    private static JsonObject AvatarSet(XElement node)
    {
        var output = new JsonObject { ["id"] = Str(node, "id") };

        if (Attr(node, "main") == "1")
            output["main"] = true;

        if (node.Elements("avatarset").Any())
            output["avatarSets"] = Array(node.Elements("avatarset"), AvatarSet);

        if (node.Elements("bodypart").Any())
            output["bodyParts"] = Array(
                node.Elements("bodypart"),
                part => new JsonObject { ["id"] = Str(part, "id") }
            );

        return output;
    }

    private static JsonObject BodyPart(XElement part)
    {
        var output = new JsonObject
        {
            ["id"] = Str(part, "id"),
            ["x"] = Num(part, "x"),
            ["y"] = Num(part, "y"),
            ["z"] = Num(part, "z"),
            ["radius"] = Num(part, "radius"),
        };

        Optional(output, "orderBefore", part, "order-before");

        if (part.Elements("item").Any())
            output["items"] = Array(
                part.Elements("item"),
                item => new JsonObject
                {
                    ["id"] = Str(item, "id"),
                    ["x"] = Num(item, "x"),
                    ["y"] = Num(item, "y"),
                    ["z"] = Num(item, "z"),
                    ["radius"] = Num(item, "radius"),
                    ["nx"] = Num(item, "nx"),
                    ["ny"] = Num(item, "ny"),
                    ["nz"] = Num(item, "nz"),
                    ["double"] = Attr(item, "double") == "1",
                }
            );

        return output;
    }

    /// <summary>The root element, which must be the one named; the XML's comments count for nothing.</summary>
    private static XElement Root(string xml, string? name)
    {
        XDocument document;

        try
        {
            using var reader = XmlReader.Create(
                new StringReader(xml),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreComments = true,
                }
            );

            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"The avatar table is not XML: {ex.Message}", ex);
        }

        if (document.Root is not { } root || (name is not null && root.Name.LocalName != name))
            throw new FormatException($"The avatar table has no <{name}>.");

        return root;
    }

    private static JsonArray Array(IEnumerable<XElement> elements, Func<XElement, JsonNode> map) =>
        [.. elements.Select(map)];

    private static string Str(XElement element, string name) => Attr(element, name) ?? string.Empty;

    private static JsonNode? Num(XElement element, string name) =>
        JsValues.Number(JsValues.ToNumber(Attr(element, name)));

    /// <summary>A child element's text as a number (<c>&lt;camera&gt;&lt;x&gt;0&lt;/x&gt;</c>); 0 without it.</summary>
    private static JsonNode? Text(XElement? element, string name) =>
        element?.Element(name) is { } child
            ? JsValues.Number(JsValues.ToNumber(child.Value))
            : JsValues.Number(0);

    private static void Optional(JsonObject output, string key, XElement element, string name)
    {
        if (Attr(element, name) is { } value)
            output[key] = value;
    }

    /// <summary><c>repeats</c>, only when the frame gives one.</summary>
    private static void Repeats(JsonObject output, XElement frame)
    {
        if (Attr(frame, "repeats") is { Length: > 0 } repeats)
            output["repeats"] = JsValues.Number(JsValues.ToNumber(repeats));
    }
}
