using Turbo.Primitives.Messages.Outgoing.Nux;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Nux;

/// <summary>
/// AS3 <c>NewUserExperienceGiftOfferParser</c>: a count of steps, each its day, its step and a
/// count of gifts; each gift its thumbnail and a count of products; each product its code and
/// text key (empty for none).
/// </summary>
internal class NewUserExperienceGiftOfferEventMessageComposerSerializer(int header)
    : AbstractSerializer<NewUserExperienceGiftOfferEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NewUserExperienceGiftOfferEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Steps.Length);

        foreach (var step in message.Steps)
        {
            packet.WriteInteger(step.DayIndex);
            packet.WriteInteger(step.StepIndex);
            packet.WriteInteger(step.Options.Length);

            foreach (var option in step.Options)
            {
                packet.WriteString(option.ThumbnailUrl);
                packet.WriteInteger(option.Products.Length);

                foreach (var product in option.Products)
                {
                    packet.WriteString(product.ProductCode);
                    packet.WriteString(product.LocalizationKey);
                }
            }
        }
    }
}
