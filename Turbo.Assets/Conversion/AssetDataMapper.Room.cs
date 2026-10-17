using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Turbo.Assets.Conversion;

/// <summary>
/// The room content library's <c>room_visualization</c> (<c>&lt;visualizationData&gt;</c>): how the
/// room's floors, walls, landscapes and wall ads are drawn - their planes, materials and textures -
/// and the window masks, as nitro-studio's converter maps it (<c>RoomVisualizationMapper</c>,
/// <c>core/xml/asset/room-visualization</c>), into <c>roomVisualization</c>. The 32-pixel sizes are
/// left out, as every small asset is.
/// </summary>
public static partial class AssetDataMapper
{
    private static readonly string[] SMALL_PLANE_PREFIXES =
    [
        "floor_32_",
        "wall_32_",
        "landscape_32_",
        "wallad_32_",
    ];

    private static void MapRoomVisualization(XElement root, JsonObject output)
    {
        if (root.Name.LocalName != "visualizationData")
            return;

        var mapped = new JsonObject();

        foreach (var key in new[] { "floorData", "wallData", "landscapeData", "wallAdData" })
        {
            if (root.Element(key) is { } data)
                mapped[key] = MapPlaneVisualizationData(data);
        }

        if (root.Element("maskData") is { } maskData)
            mapped["maskData"] = MapPlaneMaskData(maskData);

        output["roomVisualization"] = mapped;
    }

    private static JsonObject MapPlaneVisualizationData(XElement data)
    {
        var output = new JsonObject();

        // A data has one kind of plane: the first of these it has.
        var planes = new[]
        {
            ("walls", "wall"),
            ("floors", "floor"),
            ("landscapes", "landscape"),
            ("wallAds", "wallAd"),
        }
            .Select(x =>
                data.Elements(x.Item1).Any()
                    ? data.Elements(x.Item1).Elements(x.Item2).ToList()
                    : null
            )
            .FirstOrDefault(x => x is not null);

        if (planes is { Count: > 0 })
            output["planes"] = new JsonArray([.. planes.Select(MapPlane)]);

        var materials = data.Elements("materials")
            .Elements("material")
            .Where(x => !IsSmallPlaneAsset(Attr(x, "id")))
            .ToList();

        if (materials.Count > 0)
            output["materials"] = new JsonArray([.. materials.Select(MapMaterial)]);

        var textures = data.Elements("textures")
            .Elements("texture")
            .Where(x => !IsSmallPlaneAsset(Attr(x, "id")))
            .ToList();

        if (textures.Count > 0)
            output["textures"] = new JsonArray([.. textures.Select(MapTexture)]);

        return output;
    }

    private static bool IsSmallPlaneAsset(string? id) =>
        id is not null && SMALL_PLANE_PREFIXES.Any(x => id.StartsWith(x, StringComparison.Ordinal));

    private static JsonNode MapPlane(XElement plane)
    {
        var output = new JsonObject();

        if (Attr(plane, "id") is { } id)
            output["id"] = id;

        var visualizations = MapPlaneVisualizations(plane.Elements("visualization"));

        if (visualizations.Count > 0)
            output["visualizations"] = visualizations;

        var animated = MapPlaneVisualizations(plane.Elements("animatedVisualization"));

        if (animated.Count > 0)
            output["animatedVisualization"] = animated;

        return output;
    }

    private static JsonArray MapPlaneVisualizations(
        System.Collections.Generic.IEnumerable<XElement> visualizations
    )
    {
        var list = new JsonArray();

        foreach (var visualization in visualizations)
        {
            var size = Attr(visualization, "size") is { } s ? JsValues.ParseInt(s) : null;

            if (size == 32)
                continue;

            var output = new JsonObject();

            if (Attr(visualization, "size") is { } sizeText)
                output["size"] = JsValues.Int(sizeText);

            if (Attr(visualization, "horizontalAngle") is { } horizontal)
                output["horizontalAngle"] = JsValues.Float(horizontal);

            if (Attr(visualization, "verticalAngle") is { } vertical)
                output["verticalAngle"] = JsValues.Float(vertical);

            // In document order: a landscape's clouds may sit between two of its layers.
            var layers = new JsonArray();

            foreach (var child in visualization.Elements())
            {
                if (child.Name.LocalName == "visualizationLayer")
                    layers.Add(MapPlaneLayer(child));
                else if (child.Name.LocalName == "animationLayer")
                    layers.Add(MapAnimatedLayer(child));
            }

            if (layers.Count > 0)
                output["allLayers"] = layers;

            list.Add(output);
        }

        return list;
    }

    private static JsonNode MapPlaneLayer(XElement layer)
    {
        var output = new JsonObject();

        if (Attr(layer, "materialId") is { } material)
            output["materialId"] = material;

        if (Attr(layer, "color") is { } color)
            output["color"] = JsValues.Int(color);

        if (Attr(layer, "offset") is { } offset)
            output["offset"] = JsValues.Int(offset);

        if (Attr(layer, "align") is { } align)
            output["align"] = align;

        return output;
    }

    private static JsonNode MapAnimatedLayer(XElement layer)
    {
        var output = new JsonObject();
        var items = new JsonArray();

        foreach (var item in layer.Elements("animationItem"))
        {
            var mapped = new JsonObject();

            if (Attr(item, "id") is { } id)
                mapped["id"] = JsValues.Int(id);

            if (Attr(item, "assetId") is { } asset)
                mapped["assetId"] = asset;

            // Kept as the text it is, as the Studio's converter keeps these four.
            foreach (var key in new[] { "x", "y", "randomX", "randomY" })
            {
                if (Attr(item, key) is { } value)
                    mapped[key] = value;
            }

            if (Attr(item, "speedX") is { } speedX)
                mapped["speedX"] = JsValues.Float(speedX);

            if (Attr(item, "speedY") is { } speedY)
                mapped["speedY"] = JsValues.Float(speedY);

            items.Add(mapped);
        }

        if (items.Count > 0)
            output["items"] = items;

        return output;
    }

    private static JsonNode MapMaterial(XElement material)
    {
        var output = new JsonObject();

        if (Attr(material, "id") is { } id)
            output["id"] = id;

        var matrices = new JsonArray();

        foreach (var matrix in material.Elements("materialCellMatrix"))
        {
            var mapped = new JsonObject();

            if (Attr(matrix, "repeatMode") is { } repeat)
                mapped["repeatMode"] = repeat;

            if (Attr(matrix, "align") is { } align)
                mapped["align"] = align;

            foreach (var key in new[] { "normalMinX", "normalMaxX", "normalMinY", "normalMaxY" })
            {
                if (Attr(matrix, key) is { } value)
                    mapped[key] = JsValues.Float(value);
            }

            var columns = new JsonArray();

            foreach (var column in matrix.Elements("materialCellColumn"))
            {
                var mappedColumn = new JsonObject();

                if (Attr(column, "repeatMode") is { } columnRepeat)
                    mappedColumn["repeatMode"] = columnRepeat;

                if (Attr(column, "width") is { } width)
                    mappedColumn["width"] = JsValues.Int(width);

                var cells = new JsonArray([
                    .. column.Elements("materialCell").Select(MapMaterialCell),
                ]);

                if (cells.Count > 0)
                    mappedColumn["cells"] = cells;

                columns.Add(mappedColumn);
            }

            if (columns.Count > 0)
                mapped["columns"] = columns;

            matrices.Add(mapped);
        }

        if (matrices.Count > 0)
            output["matrices"] = matrices;

        return output;
    }

    private static JsonNode MapMaterialCell(XElement cell)
    {
        var output = new JsonObject();

        if (Attr(cell, "textureId") is { } texture)
            output["textureId"] = texture;

        if (cell.Element("extraItemData") is { } extra)
        {
            var mapped = new JsonObject();

            if (Attr(extra, "limitMax") is { } limit)
                mapped["limitMax"] = JsValues.Int(limit);

            if (extra.Elements("extraItemTypes").Any())
                mapped["extraItemTypes"] = new JsonArray([
                    .. extra
                        .Elements("extraItemTypes")
                        .Elements("extraItemType")
                        .Select(x => Attr(x, "assetName"))
                        .OfType<string>()
                        .Select(x => (JsonNode)x),
                ]);

            if (extra.Elements("offsets").Any())
                mapped["offsets"] = new JsonArray([
                    .. extra
                        .Elements("offsets")
                        .Elements("offset")
                        .Select(x =>
                            (JsonNode)
                                new JsonArray(
                                    JsValues.Number(
                                        Attr(x, "x") is { } ox ? JsValues.ParseInt(ox) : 0
                                    ),
                                    JsValues.Number(
                                        Attr(x, "y") is { } oy ? JsValues.ParseInt(oy) : 0
                                    )
                                )
                        ),
                ]);

            output["extraData"] = mapped;
        }

        return output;
    }

    private static JsonNode MapTexture(XElement texture)
    {
        var output = new JsonObject();

        if (Attr(texture, "id") is { } id)
            output["id"] = id;

        var bitmaps = MapTextureBitmaps(texture.Elements("bitmap"));

        if (bitmaps.Count > 0)
            output["bitmaps"] = bitmaps;

        return output;
    }

    private static JsonArray MapTextureBitmaps(
        System.Collections.Generic.IEnumerable<XElement> bitmaps
    )
    {
        var list = new JsonArray();

        foreach (var bitmap in bitmaps)
        {
            var output = new JsonObject();

            if (Attr(bitmap, "assetName") is { } asset)
                output["assetName"] = asset;

            foreach (var key in new[] { "normalMinX", "normalMaxX", "normalMinY", "normalMaxY" })
            {
                if (Attr(bitmap, key) is { } value)
                    output[key] = JsValues.Float(value);
            }

            list.Add(output);
        }

        return list;
    }

    private static JsonObject MapPlaneMaskData(XElement maskData)
    {
        var output = new JsonObject();
        var masks = maskData.Elements("mask").ToList();

        if (masks.Count == 0)
            return output;

        var list = new JsonArray();

        foreach (var mask in masks)
        {
            var mapped = new JsonObject();

            if (Attr(mask, "id") is { } id)
                mapped["id"] = id;

            var visualizations = new JsonArray();

            foreach (var visualization in mask.Elements("maskVisualization"))
            {
                if (Attr(visualization, "size") is { } s && JsValues.ParseInt(s) == 32)
                    continue;

                var mappedVisualization = new JsonObject();

                if (Attr(visualization, "size") is { } size)
                    mappedVisualization["size"] = JsValues.Int(size);

                var bitmaps = MapTextureBitmaps(visualization.Elements("bitmap"));

                if (bitmaps.Count > 0)
                    mappedVisualization["bitmaps"] = bitmaps;

                visualizations.Add(mappedVisualization);
            }

            if (visualizations.Count > 0)
                mapped["visualizations"] = visualizations;

            list.Add(mapped);
        }

        output["masks"] = list;

        return output;
    }
}
