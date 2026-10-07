using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Xunit;

namespace Turbo.Tests.Furniture;

/// <summary>
/// A section written to an item's extra data reads back the same: the writer names properties
/// camelCase, and a reader that matched names exactly found none of them, so a rent, a claim or
/// a contract that was saved read back as never having been set.
/// </summary>
public class ExtraDataSectionTests
{
    [Fact]
    public void A_written_section_reads_back()
    {
        var extraData = new ExtraData(null);

        extraData.UpdateSection(
            RentableSpaceData.SECTION,
            new RentableSpaceData
            {
                RenterId = 9,
                RenterName = "renter",
                ExpiresAt = 5,
            }
        );

        var read = FurnitureExtraDataSections.Read<RentableSpaceData>(
            extraData,
            RentableSpaceData.SECTION,
            NullLogger.Instance
        );

        read.Should().NotBeNull();
        read!.RenterId.Should().Be(9);
        read.RenterName.Should().Be("renter");
    }

    [Fact]
    public void A_section_stored_with_pascal_case_names_still_reads()
    {
        var extraData = new ExtraData(
            """{"rentable_space":{"RenterId":4,"RenterName":"old","ExpiresAt":1}}"""
        );

        FurnitureExtraDataSections
            .Read<RentableSpaceData>(extraData, RentableSpaceData.SECTION, NullLogger.Instance)!
            .RenterId.Should()
            .Be(4);
    }
}
