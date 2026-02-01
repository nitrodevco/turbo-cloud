using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

[RoomObjectLogic("wf_slc_furni_neighborhood")]
public class WiredSelectorItemsInNeighborhood(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_IN_NEIGHBORHOOD;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [WiredPlayerSourceType.TriggeredUser, WiredPlayerSourceType.SignalUsers],
        ];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // merged source
            new WiredParamRule(0), // rootX
            new WiredParamRule(0), // rootY
        ];

    public override IWiredParamRule? GetIntParamTailRule() => new WiredParamRule(0);

    public override async Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = await ctx.GetWiredSelectionSetAsync(this, ct);
        var output = new WiredSelectionSet();

        foreach (var id in input.SelectedFurniIds)
        {
            try
            {
                if (
                    !_roomGrain._state.ItemsById.TryGetValue(id, out var item)
                    || item is not IRoomFloorItem floorItem
                )
                    continue;

                var tileIds = MaskToTiles(
                        floorItem.X,
                        floorItem.Y,
                        _wiredData.GetIntParam<int>(1),
                        _wiredData.GetIntParam<int>(2),
                        _roomGrain._roomConfig.WiredNeighborhoodRadius,
                        _roomGrain.MapModule.Width,
                        _roomGrain.MapModule.Height,
                        [.. _wiredData.IntParams[3..]]
                    )
                    .ToList();

                foreach (var tileId in tileIds)
                {
                    try
                    {
                        var itemIds = _roomGrain._state.TileFloorStacks[tileId];

                        foreach (var itemId in itemIds)
                            output.SelectedFurniIds.Add((int)itemId);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                continue;
            }
        }

        foreach (var id in input.SelectedPlayerIds)
        {
            try
            {
                if (
                    !_roomGrain._state.AvatarsByPlayerId.TryGetValue(id, out var objectId)
                    || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
                )
                    continue;

                var tileIds = MaskToTiles(
                        avatar.X,
                        avatar.Y,
                        _wiredData.GetIntParam<int>(1),
                        _wiredData.GetIntParam<int>(2),
                        _roomGrain._roomConfig.WiredNeighborhoodRadius,
                        _roomGrain.MapModule.Width,
                        _roomGrain.MapModule.Height,
                        [.. _wiredData.IntParams[3..]]
                    )
                    .ToList();

                foreach (var tileId in tileIds)
                {
                    try
                    {
                        var itemIds = _roomGrain._state.TileFloorStacks[tileId];

                        foreach (var itemId in itemIds)
                            output.SelectedFurniIds.Add((int)itemId);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                continue;
            }
        }

        return output;
    }

    private static IEnumerable<int> MaskToTiles(
        int startX,
        int startY,
        int rootX,
        int rootY,
        int radius,
        int width,
        int height,
        int[] masks
    )
    {
        var size = radius * 2 + 1;
        var totalBits = size * size;
        var mask = IntParamsToBoolMask(masks, totalBits);
        var spiral = WalkSpiral(radius);

        foreach (var e in spiral)
        {
            if (!mask[e.Rank])
                continue;

            int x = startX + e.Dx + -rootX;
            int y = startY + -e.Dy + -rootY;

            if ((uint)x >= (uint)width || (uint)y >= (uint)height)
                continue;

            yield return (y * width) + x;
        }
    }

    private readonly record struct SpiralEntry(short Dx, short Dy, int Rank);

    private static List<SpiralEntry> WalkSpiral(int radius)
    {
        int size = radius * 2 + 1;
        int total = size * size;

        var result = new List<SpiralEntry>(total);

        int x = 0;
        int y = 0;
        int rank = 0;

        result.Add(new SpiralEntry(0, 0, rank++));

        if (total > 1)
        {
            int stepLen = 1;

            while (rank < total)
            {
                for (int i = 0; i < stepLen && rank < total; i++)
                {
                    x++;
                    result.Add(new((short)x, (short)y, rank++));
                }

                for (int i = 0; i < stepLen && rank < total; i++)
                {
                    y++;
                    result.Add(new((short)x, (short)y, rank++));
                }

                stepLen++;

                for (int i = 0; i < stepLen && rank < total; i++)
                {
                    x--;
                    result.Add(new((short)x, (short)y, rank++));
                }

                for (int i = 0; i < stepLen && rank < total; i++)
                {
                    y--;
                    result.Add(new((short)x, (short)y, rank++));
                }

                stepLen++;
            }
        }

        return result;
    }

    private static bool[] IntParamsToBoolMask(int[] intParams, int totalBits)
    {
        var mask = new bool[totalBits];
        int bitIndex = 0;

        for (int i = 0; i < intParams.Length && bitIndex < totalBits; i++)
        {
            uint v = unchecked((uint)intParams[i]);

            byte b0 = (byte)(v & 0xFF);
            byte b1 = (byte)((v >> 8) & 0xFF);
            byte b2 = (byte)((v >> 16) & 0xFF);
            byte b3 = (byte)((v >> 24) & 0xFF);

            bitIndex = UnpackByte(b0, mask, bitIndex, totalBits);
            bitIndex = UnpackByte(b1, mask, bitIndex, totalBits);
            bitIndex = UnpackByte(b2, mask, bitIndex, totalBits);
            bitIndex = UnpackByte(b3, mask, bitIndex, totalBits);
        }

        return mask;
    }

    private static int UnpackByte(byte b, bool[] mask, int bitIndex, int totalBits)
    {
        for (int bit = 0; bit < 8 && bitIndex < totalBits; bit++)
            mask[bitIndex++] = ((b >> bit) & 1) != 0;

        return bitIndex;
    }
}
