using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Migrations;
using Turbo.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>The vending section the migration writes for each hand-item furni is what the logic reads.</summary>
public sealed class VendingMigrationTests
{
    private static (string Name, int Usage, string Vending)[] Rows() =>
        ((string, int, string)[])
            typeof(MapVendingFurni)
                .GetField("VENDING", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

    private static VendingMachineData Read(string vending) =>
        FurnitureExtraDataSections.Read<VendingMachineData>(
            new ExtraData(null),
            $$"""{"{{VendingMachineData.SECTION}}":{{vending}}}""",
            VendingMachineData.SECTION,
            NullLogger.Instance
        )!;

    [Fact]
    public void Every_vending_section_reads_with_hand_items_to_give()
    {
        var rows = Rows();

        rows.Should().HaveCount(280);
        rows.Select(x => x.Name).Should().OnlyHaveUniqueItems();

        foreach (var row in rows)
        {
            Read(row.Vending).HandItems.Should().NotBeEmpty(row.Name).And.OnlyContain(x => x > 0);
            row.Usage.Should().BeOneOf([1, 2], row.Name);
        }
    }

    [Fact]
    public void The_pura_fridge_gives_its_four_items_and_animates()
    {
        var fridge = Read(Rows().Single(x => x.Name == "fridge").Vending);

        fridge.HandItems.Should().Equal(3, 4, 5, 6);
        fridge.Animates.Should().BeTrue();
    }
}
