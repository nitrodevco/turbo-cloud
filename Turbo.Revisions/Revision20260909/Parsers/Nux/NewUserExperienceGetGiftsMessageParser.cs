using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Nux;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Onboarding.Snapshots;

namespace Turbo.Revisions.Revision20260909.Parsers.Nux;

/// <summary>
/// AS3 <c>NewUserExperienceGetGiftsMessageComposer</c>: the count of the ints that follow (three
/// per pick, not the number of picks), then each pick's day, step and gift index.
/// </summary>
internal class NewUserExperienceGetGiftsMessageParser : IParser
{
    private const int INTS_PER_SELECTION = 3;

    public IMessageEvent Parse(IClientPacket packet)
    {
        var count = packet.PopCount(sizeof(int)) / INTS_PER_SELECTION;
        var selections = ImmutableArray.CreateBuilder<NuxGiftSelectionSnapshot>(count);

        for (var i = 0; i < count; i++)
        {
            selections.Add(
                new NuxGiftSelectionSnapshot
                {
                    DayIndex = packet.PopInt(),
                    StepIndex = packet.PopInt(),
                    GiftIndex = packet.PopInt(),
                }
            );
        }

        return new NewUserExperienceGetGiftsMessage { Selections = selections.MoveToImmutable() };
    }
}
