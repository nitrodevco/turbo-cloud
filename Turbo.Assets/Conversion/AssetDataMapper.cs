using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace Turbo.Assets.Conversion;

/// <summary>
/// A library's XML documents as the asset data a <c>.nitro</c> carries (nitro-api's <c>IAssetData</c>):
/// the index, manifest, animation (an avatar effect's), assets, logic and visualization, and the
/// palettes' colours. The JSON is
/// written as the studio's converter wrote it - its keys in the same order, a value it would leave
/// out left out, a number it could not read <c>null</c> - since the client reads what it wrote.
/// <para>
/// Not mapped: <c>room_visualization</c>, which only the room library has.
/// </para>
/// </summary>
public static class AssetDataMapper
{
    // Never drawn by the client, so never in a bundle.
    private const int SKIPPED_VISUALIZATION_SIZE = 32;

    private static readonly XmlReaderSettings XML_SETTINGS = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
    };

    public static JsonObject Map(IAssetLibrary library)
    {
        var output = new JsonObject();

        if (Parse(library.GetXml("index")) is { } index)
        {
            SetText(output, "type", index, "type");
            SetText(output, "logicType", index, "logic");
            SetText(output, "visualizationType", index, "visualization");
        }

        if (!output.ContainsKey("type"))
            output["type"] = library.DocumentClass;

        var type = output["type"]!.GetValue<string>();

        if (Parse(library.GetXml("manifest")) is { } manifest)
            MapManifest(manifest, output);

        // After the manifest, before the assets, so its key lands where the studio's did.
        if (Parse(library.GetXml("animation")) is { Name.LocalName: "animation" } animation)
            output["animations"] = new JsonArray(MapEffectAnimation(animation));

        var assets =
            Parse(library.GetXml($"{type}_assets")) ?? Parse(library.GetXml($"{type}_room_assets"));

        if (assets is not null)
            MapAssets(assets, output);

        if (Parse(library.GetXml($"{type}_logic")) is { } logic)
            output["logic"] = MapLogic(logic);

        if (Parse(library.GetXml($"{type}_visualization")) is { } visualization)
            MapVisualization(visualization, output);

        if (output["palettes"] is JsonArray palettes)
            AddPaletteColors(library, type, palettes);

        return output;
    }

    // ------------------------------------------------------------------ manifest

    private static void MapManifest(XElement manifest, JsonObject output)
    {
        if (manifest.Element("library") is not { } library)
            return;

        var assets = library.Elements("assets").SelectMany(x => x.Elements("asset")).ToList();
        var aliases = library.Elements("aliases").SelectMany(x => x.Elements("alias")).ToList();

        if (assets.Count > 0)
        {
            var list = new JsonArray();

            foreach (var asset in assets)
            {
                var param = asset.Element("param");

                if (Attr(asset, "mimeType") != "image/png" || param is null)
                    continue;

                var item = new JsonObject();

                SetText(item, "name", asset, "name");

                if (Attr(param, "value") is { } value)
                {
                    var split = value.Split(',');

                    item["x"] = JsValues.Int(split[0]);
                    item["y"] = split.Length > 1 ? JsValues.Int(split[1]) : null;
                }

                list.Add(item);
            }

            output["assets"] = list;
        }

        if (aliases.Count > 0)
        {
            var list = new JsonArray();

            foreach (var alias in aliases)
            {
                var item = new JsonObject();

                SetText(item, "name", alias, "name");
                SetText(item, "link", alias, "link");

                if (Attr(alias, "fliph") == "1")
                    item["flipH"] = true;

                if (Attr(alias, "flipv") == "1")
                    item["flipV"] = true;

                list.Add(item);
            }

            output["aliases"] = list;
        }
    }

    // ------------------------------------------------------------------ animation

    /// <summary>
    /// An avatar effect's <c>&lt;animation&gt;</c> (the client's <c>AnimationData</c>): its sprites,
    /// frames, avatar layering and overrides. A list is written only when the element has one.
    /// </summary>
    private static JsonObject MapEffectAnimation(XElement animation)
    {
        var output = new JsonObject();

        SetText(output, "name", animation, "name");
        SetText(output, "desc", animation, "desc");

        if (Attr(animation, "resetOnToggle") is { } reset)
            output["resetOnToggle"] = reset == "true";

        SetList(
            output,
            "directions",
            animation,
            "direction",
            x =>
            {
                var item = new JsonObject();

                SetInt(item, "offset", x, "offset");

                return item;
            }
        );
        SetList(output, "shadows", animation, "shadow", x => Ids(x));
        SetList(
            output,
            "adds",
            animation,
            "add",
            x =>
            {
                var item = new JsonObject();

                SetText(item, "id", x, "id");
                SetText(item, "align", x, "align");
                SetText(item, "blend", x, "blend");
                SetInt(item, "ink", x, "ink");
                SetText(item, "base", x, "base");

                return item;
            }
        );
        SetList(output, "removes", animation, "remove", x => Ids(x));
        SetList(output, "sprites", animation, "sprite", MapEffectSprite);
        SetList(output, "frames", animation, "frame", MapEffectFrame);
        SetList(
            output,
            "avatars",
            animation,
            "avatar",
            x =>
            {
                var item = new JsonObject();

                SetText(item, "background", x, "background");
                SetText(item, "foreground", x, "foreground");
                SetInt(item, "ink", x, "ink");

                return item;
            }
        );
        SetList(
            output,
            "overrides",
            animation,
            "override",
            x =>
            {
                var item = new JsonObject();

                SetText(item, "name", x, "name");
                SetText(item, "override", x, "override");
                SetList(item, "frames", x, "frame", MapEffectFrame);

                return item;
            }
        );

        return output;
    }

    private static JsonObject MapEffectSprite(XElement sprite)
    {
        var output = new JsonObject();

        SetText(output, "id", sprite, "id");
        SetInt(output, "directions", sprite, "directions");
        SetText(output, "member", sprite, "member");
        SetInt(output, "ink", sprite, "ink");
        SetInt(output, "staticY", sprite, "staticY");
        SetList(
            output,
            "directionList",
            sprite,
            "direction",
            x =>
            {
                var item = new JsonObject();

                SetInt(item, "id", x, "id");
                SetInt(item, "dx", x, "dx");
                SetInt(item, "dy", x, "dy");
                SetInt(item, "dz", x, "dz");

                return item;
            }
        );

        return output;
    }

    private static JsonObject MapEffectFrame(XElement frame)
    {
        var output = new JsonObject();

        SetInt(output, "repeats", frame, "repeats");
        SetList(output, "fxs", frame, "fx", MapEffectFramePart);
        SetList(output, "bodyparts", frame, "bodypart", MapEffectFramePart);

        return output;
    }

    private static JsonObject MapEffectFramePart(XElement part)
    {
        var output = new JsonObject();

        SetText(output, "id", part, "id");
        SetInt(output, "frame", part, "frame");
        SetText(output, "base", part, "base");
        SetText(output, "action", part, "action");
        SetInt(output, "dx", part, "dx");
        SetInt(output, "dy", part, "dy");
        SetInt(output, "dz", part, "dz");
        SetInt(output, "dd", part, "dd");
        SetList(
            output,
            "items",
            part,
            "item",
            x =>
            {
                var item = new JsonObject();

                SetText(item, "id", x, "id");
                SetText(item, "base", x, "base");

                return item;
            }
        );

        return output;
    }

    private static JsonObject Ids(XElement element)
    {
        var output = new JsonObject();

        SetText(output, "id", element, "id");

        return output;
    }

    /// <summary>The <paramref name="child"/> elements mapped, under <paramref name="key"/> - only when there is one.</summary>
    private static void SetList(
        JsonObject output,
        string key,
        XElement element,
        string child,
        Func<XElement, JsonObject> map
    )
    {
        var children = element.Elements(child).ToList();

        if (children.Count > 0)
            output[key] = new JsonArray([.. children.Select(x => (JsonNode?)map(x))]);
    }

    // ------------------------------------------------------------------ assets

    private static void MapAssets(XElement assets, JsonObject output)
    {
        var assetList = assets.Elements("asset").ToList();
        var paletteList = assets.Elements("palette").ToList();

        if (assetList.Count > 0)
        {
            var list = new JsonArray();

            foreach (var asset in assetList)
            {
                var item = new JsonObject();

                SetText(item, "name", asset, "name");
                SetText(item, "source", asset, "source");
                item["x"] = Attr(asset, "x") is { } x ? JsValues.Int(x) : 0;
                item["y"] = Attr(asset, "y") is { } y ? JsValues.Int(y) : 0;
                SetFlag(item, "flipH", asset, "flipH");
                SetFlag(item, "flipV", asset, "flipV");
                SetFlag(item, "usesPalette", asset, "usesPalette");
                list.Add(item);
            }

            output["assets"] = list;
        }

        if (paletteList.Count > 0)
        {
            var list = new JsonArray();

            foreach (var palette in paletteList)
            {
                var item = new JsonObject();

                SetInt(item, "id", palette, "id");
                SetText(item, "source", palette, "source");

                if (Attr(palette, "master") is { } master)
                    item["master"] = master == "true";

                if (Attr(palette, "tags") is { } tags)
                    item["tags"] = new JsonArray([.. tags.Split(',').Select(x => (JsonNode?)x)]);

                SetInt(item, "breed", palette, "breed");
                SetInt(item, "colorTag", palette, "colortag");
                SetText(item, "color1", palette, "color1");
                SetText(item, "color2", palette, "color2");
                list.Add(item);
            }

            output["palettes"] = list;
        }
    }

    /// <summary>A palette's colours from its binary; a palette without one is dropped, as the client drops it.</summary>
    private static void AddPaletteColors(IAssetLibrary library, string type, JsonArray palettes)
    {
        foreach (var palette in palettes.OfType<JsonObject>().ToList())
        {
            var source = palette["source"]?.GetValue<string>();
            var data = source is null ? null : library.GetBinary($"{type}_{source}");

            if (data is null || data.Length < 3)
            {
                palettes.Remove(palette);

                continue;
            }

            var colors = new JsonArray();

            for (var i = 0; i + 2 < data.Length; i += 3)
                colors.Add(new JsonArray(data[i], data[i + 1], data[i + 2]));

            palette["rgb"] = colors;
        }
    }

    // ------------------------------------------------------------------ logic

    private static JsonObject MapLogic(XElement logic)
    {
        var output = new JsonObject();

        if (logic.Element("model") is { } model)
        {
            var mapped = new JsonObject();

            if (model.Element("dimensions") is { } dimensions)
            {
                var size = new JsonObject();

                SetFloat(size, "x", dimensions, "x");
                SetFloat(size, "y", dimensions, "y");
                SetFloat(size, "z", dimensions, "z");
                SetFloat(size, "centerZ", dimensions, "centerZ");
                mapped["dimensions"] = size;
            }

            // A model with a <directions> element: none listed is the one direction 0.
            if (model.Elements("directions").Any())
            {
                var ids = model
                    .Elements("directions")
                    .SelectMany(x => x.Elements("direction"))
                    .Select(x => Attr(x, "id"))
                    .OfType<string>()
                    .Select(JsValues.Int)
                    .ToList();

                mapped["directions"] = ids.Count == 0 ? new JsonArray(0) : new JsonArray([.. ids]);
            }

            output["model"] = mapped;
        }

        if (logic.Element("action") is { } action)
        {
            var mapped = new JsonObject();

            SetText(mapped, "link", action, "link");
            SetInt(mapped, "startState", action, "startState");
            output["action"] = mapped;
        }

        if (logic.Element("mask") is { } mask)
            SetText(output, "maskType", mask, "type");

        if (logic.Element("credits") is { } credits)
            SetText(output, "credits", credits, "value");

        if (logic.Element("allspritesactivate") is not null)
            output["allSpritesActivate"] = true;

        if (logic.Element("sound")?.Element("sample") is { } sample)
        {
            var mapped = new JsonObject();

            SetInt(mapped, "id", sample, "id");

            if (Attr(sample, "nopitch") is { } noPitch)
                mapped["noPitch"] = noPitch == "true";

            output["soundSample"] = mapped;
        }

        if (logic.Element("planetsystem") is { } planets)
        {
            var list = new JsonArray();

            foreach (var planet in planets.Elements("object"))
            {
                var item = new JsonObject();

                SetInt(item, "id", planet, "id");
                SetText(item, "name", planet, "name");
                SetText(item, "parent", planet, "parent");
                SetFloat(item, "radius", planet, "radius");
                SetFloat(item, "arcSpeed", planet, "arcspeed");
                SetFloat(item, "arcOffset", planet, "arcoffset");
                SetFloat(item, "blend", planet, "blend");
                SetFloat(item, "height", planet, "height");
                list.Add(item);
            }

            output["planetSystems"] = list;
        }

        if (logic.Element("particlesystems") is { } particles)
            output["particleSystems"] = MapParticleSystems(particles);

        if (logic.Element("customvars") is { } customVars)
        {
            var mapped = new JsonObject();
            var variables = customVars.Elements("variable").ToList();

            if (variables.Count > 0)
                mapped["variables"] = new JsonArray([
                    .. variables
                        .Select(x => Attr(x, "name"))
                        .OfType<string>()
                        .Select(x => (JsonNode?)x),
                ]);

            output["customVars"] = mapped;
        }

        return output;
    }

    private static JsonArray MapParticleSystems(XElement systems)
    {
        var list = new JsonArray();

        foreach (var system in systems.Elements("particlesystem"))
        {
            var item = new JsonObject();

            SetInt(item, "size", system, "size");
            SetInt(item, "canvasId", system, "canvas_id");
            SetInt(item, "offsetY", system, "offset_y");
            SetFloat(item, "blend", system, "blend");
            SetText(item, "bgColor", system, "bgcolor");

            var emitters = system.Elements("emitter").ToList();

            if (emitters.Count > 0)
            {
                var mapped = new JsonArray();

                foreach (var emitter in emitters)
                {
                    var e = new JsonObject();

                    SetInt(e, "id", emitter, "id");
                    SetText(e, "name", emitter, "name");
                    SetInt(e, "spriteId", emitter, "sprite_id");
                    SetInt(e, "maxNumParticles", emitter, "max_num_particles");
                    SetInt(e, "particlesPerFrame", emitter, "particles_per_frame");

                    // One pulse unless it says otherwise - but only an emitter with attributes says anything.
                    if (emitter.HasAttributes)
                        e["burstPulse"] = Attr(emitter, "burst_pulse") is { } pulse
                            ? JsValues.Int(pulse)
                            : 1;

                    SetInt(e, "fuseTime", emitter, "fuse_time");

                    if (emitter.Element("simulation") is { } simulation)
                    {
                        var s = new JsonObject();

                        SetFloat(s, "force", simulation, "force");
                        SetFloat(s, "direction", simulation, "direction");
                        SetFloat(s, "gravity", simulation, "gravity");
                        SetFloat(s, "airFriction", simulation, "airfriction");
                        SetText(s, "shape", simulation, "shape");
                        SetFloat(s, "energy", simulation, "energy");
                        e["simulation"] = s;
                    }

                    var particles =
                        emitter.Element("particles")?.Elements("particle").ToList() ?? [];

                    if (particles.Count > 0)
                    {
                        var p = new JsonArray();

                        foreach (var particle in particles)
                        {
                            var pi = new JsonObject();

                            if (particle.HasAttributes)
                            {
                                // Anything but "false" is an emitter, a missing attribute included.
                                pi["isEmitter"] = Attr(particle, "is_emitter") != "false";
                                SetInt(pi, "lifeTime", particle, "lifetime");

                                if (Attr(particle, "fade") is { } fade)
                                    pi["fade"] = fade == "true";
                            }

                            var frames = particle.Elements("frame").ToList();

                            if (frames.Count > 0)
                                pi["frames"] = new JsonArray([
                                    .. frames.Select(x => (JsonNode?)Attr(x, "name")),
                                ]);

                            p.Add(pi);
                        }

                        e["particles"] = p;
                    }

                    mapped.Add(e);
                }

                item["emitters"] = mapped;
            }

            list.Add(item);
        }

        return list;
    }

    // ------------------------------------------------------------------ visualization

    private static void MapVisualization(XElement root, JsonObject output)
    {
        var graphics = root.Elements("graphics").ToList();

        if (graphics.Count == 0)
            return;

        // AnimatedPetVisualizationData reads disableheadturn from <graphics>.
        bool? disableHeadTurn = null;

        foreach (var graphic in graphics)
            if (Attr(graphic, "disableheadturn") is { } value)
                disableHeadTurn = value == "1";

        var visualizations = graphics.SelectMany(x => x.Elements("visualization")).ToList();

        if (visualizations.Count == 0)
            return;

        var list = new JsonArray();

        foreach (var visualization in visualizations)
        {
            var size = Attr(visualization, "size") is { } sizeText
                ? JsValues.ParseInt(sizeText)
                : null;

            if (size == SKIPPED_VISUALIZATION_SIZE)
                continue;

            var item = new JsonObject();

            SetInt(item, "angle", visualization, "angle");
            SetInt(item, "layerCount", visualization, "layerCount");
            SetInt(item, "size", visualization, "size");

            if (disableHeadTurn == true)
                item["disableHeadTurn"] = true;

            var layers = visualization
                .Elements("layers")
                .SelectMany(x => x.Elements("layer"))
                .ToList();

            if (layers.Count > 0)
                item["layers"] = MapLayers(layers);

            var directions = visualization
                .Elements("directions")
                .SelectMany(x => x.Elements("direction"))
                .ToList();

            if (directions.Count > 0)
            {
                var mapped = new JsonArray();

                foreach (var direction in directions)
                {
                    var d = new JsonObject();
                    var directionLayers = direction.Elements("layer").ToList();

                    SetInt(d, "id", direction, "id");

                    if (directionLayers.Count > 0)
                        d["layers"] = MapLayers(directionLayers);

                    mapped.Add(d);
                }

                item["directions"] = mapped;
            }

            var colors = visualization
                .Elements("colors")
                .SelectMany(x => x.Elements("color"))
                .ToList();

            if (colors.Count > 0)
            {
                var mapped = new JsonArray();

                foreach (var color in colors)
                {
                    var c = new JsonObject();
                    var colorLayers = color.Elements("colorLayer").ToList();

                    SetInt(c, "id", color, "id");

                    if (colorLayers.Count > 0)
                    {
                        var cl = new JsonArray();

                        foreach (var colorLayer in colorLayers)
                        {
                            var l = new JsonObject();

                            SetInt(l, "id", colorLayer, "id");

                            if (Attr(colorLayer, "color") is { } value)
                                l["color"] = JsValues.Number(JsValues.ParseInt(value, 16));

                            cl.Add(l);
                        }

                        c["layers"] = cl;
                    }

                    mapped.Add(c);
                }

                item["colors"] = mapped;
            }

            var animations = visualization
                .Elements("animations")
                .SelectMany(x => x.Elements("animation"))
                .ToList();

            if (animations.Count > 0)
                item["animations"] = MapAnimations(animations);

            // Only a <postures> with a <posture> in it; its default posture rides along.
            if (
                visualization.Element("postures") is { } postures
                && postures.Elements("posture").Any()
            )
            {
                var mapped = new JsonObject();

                SetText(mapped, "defaultPosture", postures, "defaultPosture");
                mapped["postures"] = MapPostures(postures.Elements("posture"));
                item["postures"] = mapped;
            }

            var gestures = visualization
                .Elements("gestures")
                .SelectMany(x => x.Elements("gesture"))
                .ToList();

            if (gestures.Count > 0)
                item["gestures"] = MapPostures(gestures);

            list.Add(item);
        }

        output["visualizations"] = list;
    }

    private static JsonArray MapLayers(IEnumerable<XElement> layers)
    {
        var list = new JsonArray();

        foreach (var layer in layers)
        {
            var item = new JsonObject();

            SetInt(item, "id", layer, "id");
            SetInt(item, "x", layer, "x");
            SetInt(item, "y", layer, "y");
            SetInt(item, "z", layer, "z");
            SetInt(item, "alpha", layer, "alpha");
            SetText(item, "ink", layer, "ink");
            SetText(item, "tag", layer, "tag");
            SetFlag(item, "ignoreMouse", layer, "ignoreMouse");
            list.Add(item);
        }

        return list;
    }

    private static JsonArray MapAnimations(IEnumerable<XElement> animations)
    {
        var list = new JsonArray();

        foreach (var animation in animations)
        {
            var item = new JsonObject();

            SetInt(item, "id", animation, "id");

            if (Attr(animation, "transitionTo") is { Length: > 0 } to)
                item["transitionTo"] = JsValues.Int(to);

            if (Attr(animation, "transitionFrom") is { Length: > 0 } from)
                item["transitionFrom"] = JsValues.Int(from);

            SetText(item, "immediateChangeFrom", animation, "immediateChangeFrom");
            SetFlag(item, "randomStart", animation, "randomStart");

            var layers = animation.Elements("animationLayer").ToList();

            if (layers.Count > 0)
            {
                var mapped = new JsonArray();

                foreach (var layer in layers)
                {
                    var l = new JsonObject();

                    SetInt(l, "id", layer, "id");
                    SetInt(l, "frameRepeat", layer, "frameRepeat");
                    SetInt(l, "loopCount", layer, "loopCount");
                    SetInt(l, "random", layer, "random");

                    var sequences = layer.Elements("frameSequence").ToList();

                    if (sequences.Count > 0)
                        l["frameSequences"] = MapFrameSequences(sequences);

                    mapped.Add(l);
                }

                item["layers"] = mapped;
            }

            list.Add(item);
        }

        return list;
    }

    private static JsonArray MapFrameSequences(IEnumerable<XElement> sequences)
    {
        var list = new JsonArray();

        foreach (var sequence in sequences)
        {
            var item = new JsonObject();
            var frames = sequence.Elements("frame").ToList();

            SetInt(item, "loopCount", sequence, "loopCount");
            SetInt(item, "random", sequence, "random");

            if (frames.Count > 0)
            {
                var mapped = new JsonArray();

                foreach (var frame in frames)
                {
                    var f = new JsonObject
                    {
                        // AnimationData reads the id with int(), so anything unparseable is 0.
                        ["id"] =
                            JsValues.ParseInt(Attr(frame, "id") ?? string.Empty) is { } id
                            && id != 0
                                ? JsValues.Number(id)
                                : 0,
                    };

                    SetInt(f, "x", frame, "x");
                    SetInt(f, "y", frame, "y");
                    SetInt(f, "randomX", frame, "randomX");
                    SetInt(f, "randomY", frame, "randomY");

                    var offsets = frame
                        .Elements("offsets")
                        .SelectMany(x => x.Elements("offset"))
                        .ToList();

                    if (offsets.Count > 0)
                    {
                        var o = new JsonArray();

                        foreach (var offset in offsets)
                        {
                            // AnimationData skips an offset with no direction.
                            if (Attr(offset, "direction") is null)
                                continue;

                            var oi = new JsonObject();

                            SetInt(oi, "direction", offset, "direction");
                            SetInt(oi, "x", offset, "x");
                            SetInt(oi, "y", offset, "y");
                            o.Add(oi);
                        }

                        f["offsets"] = o;
                    }

                    mapped.Add(f);
                }

                item["frames"] = mapped;
            }

            list.Add(item);
        }

        return list;
    }

    private static JsonArray MapPostures(IEnumerable<XElement> postures)
    {
        var list = new JsonArray();

        foreach (var posture in postures)
        {
            var item = new JsonObject();

            SetText(item, "id", posture, "id");
            SetInt(item, "animationId", posture, "animationId");
            list.Add(item);
        }

        return list;
    }

    // ------------------------------------------------------------------ helpers

    internal static XElement? Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), XML_SETTINGS);

            return XDocument.Load(reader).Root;
        }
        catch (XmlException ex)
        {
            throw new AssetFormatException("A library document is not readable XML.", ex);
        }
    }

    private static string? Attr(XElement element, string name) => element.Attribute(name)?.Value;

    private static void SetText(JsonObject output, string key, XElement element, string attribute)
    {
        if (Attr(element, attribute) is { } value)
            output[key] = value;
    }

    private static void SetInt(JsonObject output, string key, XElement element, string attribute)
    {
        if (Attr(element, attribute) is { } value)
            output[key] = JsValues.Int(value);
    }

    private static void SetFloat(JsonObject output, string key, XElement element, string attribute)
    {
        if (Attr(element, attribute) is { } value)
            output[key] = JsValues.Float(value);
    }

    private static void SetFlag(JsonObject output, string key, XElement element, string attribute)
    {
        if (Attr(element, attribute) is { } value)
            output[key] = value == "1";
    }
}
