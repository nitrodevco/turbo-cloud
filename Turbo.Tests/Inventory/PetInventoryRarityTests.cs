using FluentAssertions;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Extensions;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// The client draws the rarity plaque on a pet's inventory thumb for any rarity level of 0 or more
/// (PetsGridItem.updateRarityOverlay), and only monster plants have one. A dog sent with its
/// palette's breeding category 0 showed a "0" plaque in the pets inventory.
/// </summary>
public class PetInventoryRarityTests
{
    [Fact]
    public void Only_a_monster_plant_is_sent_with_its_rarity_level()
    {
        var data = PacketHarness.Encode(
            new PetInventoryEventMessageComposer
            {
                TotalFragments = 1,
                CurrentFragment = 0,
                Pets =
                [
                    Pet(1, PetTypes.DOG, rarityLevel: 0).ToSnapshot("owner"),
                    Pet(2, PetTypes.MONSTERPLANT, rarityLevel: 3).ToSnapshot("owner"),
                    Pet(3, PetTypes.MONSTERPLANT, rarityLevel: 0).ToSnapshot("owner"),
                ],
            }
        );

        data.PopInt().Should().Be(1);
        data.PopInt().Should().Be(0);
        data.PopInt().Should().Be(3);

        RarityOfNextPet(data).Should().Be(PetTypes.NO_RARITY_LEVEL);
        RarityOfNextPet(data).Should().Be(3);
        RarityOfNextPet(data).Should().Be(0);
        data.End.Should().BeTrue();
    }

    /// <summary>PetData.parse: id, name, the figure (type, palette, colour, breed, part count), level, rarity.</summary>
    private static int RarityOfNextPet(ClientPacket data)
    {
        data.PopInt();
        data.PopString();
        data.PopInt();
        data.PopInt();
        data.PopString();
        data.PopInt();
        data.PopInt().Should().Be(0);
        data.PopInt();

        return data.PopInt();
    }

    private static PetEntity Pet(int id, int typeId, int rarityLevel) =>
        new()
        {
            Id = id,
            PlayerEntityId = 1,
            Name = $"pet-{id}",
            TypeId = typeId,
            PaletteId = 1,
            BreedId = 1,
            Color = "FFFFFF",
            RarityLevel = rarityLevel,
        };
}
