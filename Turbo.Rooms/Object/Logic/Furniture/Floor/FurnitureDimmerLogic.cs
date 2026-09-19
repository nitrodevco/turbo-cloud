using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A moodlight. Its legacy data is <c>state,presetId,effectId,#RRGGBB,brightness</c> as the client
/// renders it; the three presets live in the item's extra data under <see cref="PRESETS_SECTION"/>.
/// Editing needs room rights, which is also when the client offers the editor.
/// </summary>
[RoomObjectLogic("dimmer")]
public class FurnitureDimmerLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const string PRESETS_SECTION = "dimmer";
    private const int STATE_FIELD = 0;
    private const int PRESET_FIELD = 1;
    private const int EFFECT_FIELD = 2;
    private const int COLOR_FIELD = 3;
    private const int BRIGHTNESS_FIELD = 4;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    // The client opens its editor on double-click and switches through ToggleDimmer.
    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (!await HasRightsAsync(ctx))
            return Reject(ctx, interaction, "no rights");

        switch (interaction)
        {
            case RequestDimmerPresetsInteraction:
                await SendPresetsAsync(ctx, ct);
                return true;

            case ToggleDimmerInteraction:
                await ToggleAsync();
                return true;

            case SaveDimmerPresetInteraction save:
                return await SavePresetAsync(ctx, save);

            default:
                return false;
        }
    }

    private async Task<bool> SavePresetAsync(ActionContext ctx, SaveDimmerPresetInteraction save)
    {
        if (
            save.PresetId < 1
            || save.PresetId > DimmerStates.PRESET_COUNT
            || save.EffectType is not (DimmerStates.EFFECT_ROOM or DimmerStates.EFFECT_BACKGROUND)
            || !DimmerStates.IsValidColor(save.Color)
            || save.Brightness is < DimmerStates.MIN_BRIGHTNESS or > DimmerStates.MAX_BRIGHTNESS
        )
            return Reject(ctx, save, "preset out of range");

        var presets = LoadPresets();

        presets[save.PresetId - 1] = new DimmerPresetSnapshot
        {
            Id = save.PresetId,
            Type = save.EffectType,
            Color = save.Color.ToUpperInvariant(),
            Brightness = save.Brightness,
        };

        _ctx.RoomObject.ExtraData.UpdateSection(PRESETS_SECTION, presets);

        if (save.Apply)
            await SetLegacyDataAsync(Compose(DimmerStates.ON, presets[save.PresetId - 1]));

        return true;
    }

    private Task ToggleAsync()
    {
        var (state, presetId, presets) = Current();
        var next = state == DimmerStates.ON ? DimmerStates.OFF : DimmerStates.ON;

        return SetLegacyDataAsync(Compose(next, presets[presetId - 1]));
    }

    private Task SendPresetsAsync(ActionContext ctx, CancellationToken ct)
    {
        var (state, presetId, presets) = Current();

        return _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new RoomDimmerPresetsMessageComposer
            {
                ItemId = _ctx.ObjectId,
                IsOn = state == DimmerStates.ON,
                SelectedPresetId = presetId,
                Presets = [.. presets],
            },
            ct
        );
    }

    /// <summary>The live state, preset and preset list, tolerant of unset or malformed data.</summary>
    private (int State, int PresetId, DimmerPresetSnapshot[] Presets) Current()
    {
        var presets = LoadPresets();
        var fields = GetLegacyString().Split(DimmerStates.SEPARATOR);

        var state =
            fields.Length > STATE_FIELD
            && int.TryParse(fields[STATE_FIELD], out var s)
            && s == DimmerStates.ON
                ? DimmerStates.ON
                : DimmerStates.OFF;
        var presetId =
            fields.Length > PRESET_FIELD
            && int.TryParse(fields[PRESET_FIELD], out var p)
            && p >= 1
            && p <= DimmerStates.PRESET_COUNT
                ? p
                : 1;

        return (state, presetId, presets);
    }

    private DimmerPresetSnapshot[] LoadPresets()
    {
        var presets = Enumerable
            .Range(1, DimmerStates.PRESET_COUNT)
            .Select(id => new DimmerPresetSnapshot
            {
                Id = id,
                Type = DimmerStates.EFFECT_ROOM,
                Color = DimmerStates.DEFAULT_COLOR,
                Brightness = DimmerStates.MAX_BRIGHTNESS,
            })
            .ToArray();

        if (!_ctx.RoomObject.ExtraData.TryGetSection(PRESETS_SECTION, out var element))
            return presets;

        List<DimmerPresetSnapshot>? stored;

        try
        {
            stored = element.Deserialize<List<DimmerPresetSnapshot>>();
        }
        catch (JsonException)
        {
            // Unreadable presets fall back to defaults; the next save rewrites the section.
            return presets;
        }

        foreach (var preset in stored ?? [])
        {
            if (preset.Id >= 1 && preset.Id <= DimmerStates.PRESET_COUNT)
                presets[preset.Id - 1] = preset;
        }

        return presets;
    }

    private static string Compose(int state, DimmerPresetSnapshot preset) =>
        string.Join(
            DimmerStates.SEPARATOR,
            state,
            preset.Id,
            preset.Type,
            preset.Color,
            preset.Brightness
        );
}
