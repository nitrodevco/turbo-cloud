using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// UserChange carries the gender in lower case: official client captures, 18 of 18 ("m" 16,
/// "f" 2), while FigureUpdate carries "M". Turbo wrote "M" / "F".
/// </summary>
public sealed class UserChangeGenderTests
{
    [Theory]
    [InlineData(AvatarGenderType.Male, "m")]
    [InlineData(AvatarGenderType.Female, "f")]
    public void The_gender_is_written_in_lower_case(AvatarGenderType gender, string written)
    {
        var packet = PacketHarness.Encode(
            new UserChangeMessageComposer
            {
                ObjectId = -1,
                Figure = "hd-180-1",
                Gender = gender,
                CustomInfo = "",
                AchievementScore = 456,
                BadgesRank = -1,
            }
        );

        (packet.PopInt(), packet.PopString(), packet.PopString())
            .Should()
            .Be((-1, "hd-180-1", written));
    }
}
