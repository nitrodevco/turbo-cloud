using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Projectile add-on's projectiles default to "All furni moved by the Wired stack"
/// (sources.furni.901; official form contract [0,100,200,201,901], default 901): every furni
/// the stack moves flies as a projectile, without picking any.
/// </summary>
public sealed class WiredProjectileSourcesTests
{
    private const int BALL = 30;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task By_default_every_furni_the_stack_moves_is_a_projectile()
    {
        var ball = _room.AddFloorItem(BALL, 1, 1);
        var projectile = _room.AddBox<WiredAddonProjectile>(1, 0, 0, "wf_xtra_rotate_to_dir");

        // Rotate in the moving direction, eight directions; everything else as the editor opens.
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                1,
                intParams:
                [
                    1,
                    (int)WiredDirectionalSystemType.EightStraight,
                    0,
                    0,
                    1,
                    (int)WiredVariableTargetType.User,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    (int)WiredProjectileDistanceType.Normal,
                    0,
                    0,
                    (int)WiredVariableTargetType.User,
                    0,
                ]
            )
        )
            .Should()
            .BeTrue();

        // 901, "All furni moved by the Wired stack".
        projectile
            .GetFurniSources()[0]
            .Select(x => (int)WiredFurniSourceTypeExtensions.GetProtocolId(x))
            .Should()
            .Equal(901);

        var processing = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        await projectile.MutatePolicyAsync(processing, Ct);

        var execution = new WiredExecutionContext(_room.Harness.Room)
        {
            Policy = processing.Policy,
            CancellationToken = Ct,
        };

        (await execution.ProcessFloorItemMovementAsync(ball, _room.Map.ToIdx(3, 1), null, null))
            .Should()
            .BeTrue();
        ball.Rotation.Should().Be(Rotation.East);
    }
}
