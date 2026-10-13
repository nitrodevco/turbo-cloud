using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// The creation wizard opens on Habbo's badge: GuildCreationInfo carried the same five layers in
/// every recording (official client capture 2026-10-13, 4 of 4): base 10 colour 3 cell 4, symbols
/// 25/17/5, 25/17/3, 29/11/4 and an empty layer. Turbo sent the first base part alone.
/// </summary>
public sealed class WizardDefaultBadgeTests
{
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GuildBadgePartDefinitionSnapshot Part(GuildBadgePartType type, int id) =>
        new()
        {
            Type = type,
            PartId = id,
            FileName = string.Empty,
            MaskFileName = string.Empty,
        };

    private static GuildColorSnapshot Colour(int id) =>
        new()
        {
            Slot = GuildColorSlotType.Badge,
            ColorId = id,
            Color = "ffffff",
        };

    private IGuildDirectoryGrain Directory(int[] bases, int[] symbols)
    {
        var grain = GrainHarness.Create(
            typeof(GuildModule).Assembly,
            "Turbo.Guilds.Grains.GuildDirectoryGrain",
            _fakes
        );
        var state = grain
            .GetType()
            .GetField(
                "_state",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .GetValue(grain)!;

        RoomHarness.SetMember(
            state,
            "EditorData",
            new GuildEditorDataSnapshot
            {
                BaseParts = [.. bases.Select(id => Part(GuildBadgePartType.Base, id))],
                SymbolParts = [.. symbols.Select(id => Part(GuildBadgePartType.Symbol, id))],
                BadgeColors = [.. new[] { 1, 3, 11, 17 }.Select(Colour)],
                PrimaryColors = [],
                SecondaryColors = [],
            }
        );

        return (IGuildDirectoryGrain)grain;
    }

    private static (int, int, int)[] Layers(ImmutableArray<GuildBadgePartSnapshot> parts) =>
        [.. parts.Select(part => (part.PartId, part.ColorId, part.Position))];

    [Fact]
    public async Task The_wizard_opens_on_Habbos_badge()
    {
        var parts = await Directory([1, 10], [25, 29]).GetDefaultBadgePartsAsync(Ct);

        Layers(parts)
            .Should()
            .Equal((10, 3, 4), (25, 17, 5), (25, 17, 3), (29, 11, 4), (0, 0, 0));
        parts[0].Type.Should().Be(GuildBadgePartType.Base);
    }

    [Fact]
    public async Task A_symbol_the_hotel_lacks_is_left_empty()
    {
        var parts = await Directory([10], [25]).GetDefaultBadgePartsAsync(Ct);

        Layers(parts)
            .Should()
            .Equal((10, 3, 4), (25, 17, 5), (25, 17, 3), (0, 0, 0), (0, 0, 0));
    }

    [Fact]
    public async Task Without_the_base_the_first_base_is_used_centred()
    {
        var parts = await Directory([1], [25, 29]).GetDefaultBadgePartsAsync(Ct);

        Layers(parts).Should().Equal((1, 1, 4));
    }
}
