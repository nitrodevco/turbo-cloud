using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A toy: the pet plays with it and learns a little. While it is played with the toy shows it
/// (state "1", back to "" after the play) and the pet plays in its type's posture with a smile
/// (official horse at a rubber ball: "kck/gst sml" + ball "1" for 3.5 s, 6 of 6).
/// </summary>
[RoomObjectLogic("pet_toy")]
public class FurniturePetToyLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetSupplyLogic(stuffDataFactory, ctx)
{
    private const string PLAYED_WITH = "1";

    public override PetSupplyType SupplyType => PetSupplyType.Toy;

    protected override async Task ServeAsync(IRoomPet pet, int left, CancellationToken ct)
    {
        var config = _roomGrain._petConfig;

        pet.AddStatus(PlayPosture(config, pet.TypeId), string.Empty);
        pet.AddStatus(AvatarStatusType.Gesture, "sml");
        pet.ActionExpiresAtMs = NowMs() + config.ToyPlayDurationMs;

        await SetLegacyDataAsync(PLAYED_WITH);

        TimerSystem.Schedule(
            _ctx.ObjectId,
            config.ToyPlayDurationMs,
            _ => SetLegacyDataAsync(string.Empty)
        );

        await PetModule.SayVocalAsync(pet, "PLAYING", ct);
        await PetModule.AddExperienceAsync(pet, config.ToyExperience, ct);
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);

        return base.OnPickupAsync(ctx, ct);
    }

    private static AvatarStatusType PlayPosture(PetConfig config, int petType)
    {
        if (config.ToyPlayPostureByType.TryGetValue(petType, out var code))
        {
            foreach (var status in Enum.GetValues<AvatarStatusType>())
            {
                if (status.ToLegacyString().Equals(code, StringComparison.OrdinalIgnoreCase))
                    return status;
            }
        }

        return AvatarStatusType.Play;
    }
}
