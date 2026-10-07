using FluentAssertions;
using Turbo.Primitives.Furniture;
using Xunit;

namespace Turbo.Tests.Furniture;

/// <summary>
/// The client decides that a furni is lent by the Builders Club from its id alone
/// (<c>com.sulake.habbo.utils.FurniId</c>): the info stand draws it as the warehouse's only when
/// the id is in its band. Every id handed out has to be there, and nothing below it may count.
/// </summary>
public sealed class FurniIdBandsTests
{
    // FurniId.isBuilderClubId: 2147418112 .. 2147483647; isTempId: 2147401728 .. 2147418111.
    private const int CLIENT_BUILDERS_CLUB_MIN = 2147418112;
    private const int CLIENT_TEMP_MIN = 2147401728;

    [Fact]
    public void EveryIdHandedOut_IsOneTheClientReadsAsBuildersClub()
    {
        FurniIdBands.BuildersClubMin.Should().Be(CLIENT_BUILDERS_CLUB_MIN);
        FurniIdBands.BuildersClubMax.Should().Be(int.MaxValue);
        FurniIdBands.IsBuildersClub(CLIENT_BUILDERS_CLUB_MIN).Should().BeTrue();
        FurniIdBands.IsBuildersClub(int.MaxValue).Should().BeTrue();
    }

    [Fact]
    public void AnIdTheClientReadsAsTemporaryOrNormal_IsNotBuildersClub()
    {
        FurniIdBands.IsBuildersClub(CLIENT_BUILDERS_CLUB_MIN - 1).Should().BeFalse();
        FurniIdBands.IsBuildersClub(CLIENT_TEMP_MIN).Should().BeFalse();
        FurniIdBands.IsBuildersClub(0x7FFE_0000).Should().BeFalse("the old band's first id");
    }
}
