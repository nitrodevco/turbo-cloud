using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Pets;

internal class PetInfoMessageComposerSerializer(int header)
    : AbstractSerializer<PetInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetInfoMessageComposer message)
    {
        var info = message.Info;

        packet
            .WriteInteger(info.PetId)
            .WriteString(info.Name)
            .WriteInteger(info.Level)
            .WriteInteger(info.MaxLevel)
            .WriteInteger(info.Experience)
            .WriteInteger(info.ExperienceRequiredToLevel)
            .WriteInteger(info.Energy)
            .WriteInteger(info.MaxEnergy)
            .WriteInteger(info.Nutrition)
            .WriteInteger(info.MaxNutrition)
            .WriteInteger(info.Respect)
            .WriteInteger(info.OwnerId)
            .WriteInteger(info.AgeDays)
            .WriteString(info.OwnerName)
            .WriteInteger(info.BreedId)
            .WriteBoolean(info.HasFreeSaddle)
            .WriteBoolean(info.IsRiding)
            .WriteInteger(info.SkillThresholds.Length);

        foreach (var threshold in info.SkillThresholds)
            packet.WriteInteger(threshold);

        packet
            .WriteInteger(info.AccessRights)
            .WriteBoolean(info.CanBreed)
            .WriteBoolean(info.CanHarvest)
            .WriteBoolean(info.CanRevive)
            .WriteInteger(info.RarityLevel)
            .WriteInteger(info.MaxWellBeingSeconds)
            .WriteInteger(info.RemainingWellBeingSeconds)
            .WriteInteger(info.RemainingGrowingSeconds)
            .WriteBoolean(info.HasBreedingPermission);
    }
}
