using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The "user clicks user" trigger and the client's half of it (Flash <c>WiredEnvironment</c>).
/// While a room has the trigger, the client is told so (<c>WiredEnvironment</c>); it then reports
/// a click on an avatar with <c>WiredClickUser</c>, does not turn its avatar itself
/// (<c>RoomObjectEventHandler.setSelectedAvatar</c> skips its <c>LookTo</c>) and holds the avatar
/// menu until the room answers (<c>AvatarInfoWidget.maybeSetupMenuView</c>). So the room turns
/// the clicker unless a trigger says "do not rotate", and answers whether the menu may open
/// unless one says "do not open avatar menu". That the trigger fires on this report rather than
/// on the plain avatar click is inferred: the client sends the report only for this trigger.
/// </summary>
public sealed partial class RoomWiredSystem
{
    // What the room last told its players, so a rebuild that changes nothing sends nothing.
    private bool _hasClickUserWired;
    private ImmutableArray<string> _enabledAchievements = [];

    /// <summary>
    /// The achievements the room's Achievement Enabler add-ons enable, in stack order, as the
    /// room last sent them in <c>WiredEnvironment</c>.
    /// </summary>
    public ImmutableArray<string> EnabledAchievements => _enabledAchievements;

    public async Task OnAvatarClickedAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    )
    {
        if (
            !AvatarModule.TryGetPlayer(ctx.PlayerId, out _)
            || !AvatarModule.TryGetAvatar(targetObjectId, out var target)
        )
            return;

        var triggers = _stacksById
            .Values.SelectMany(x => x.Triggers)
            .OfType<WiredTriggerClickUser>()
            .ToList();

        if (!triggers.Any(x => x.KeepsClickerFacing))
            await AvatarModule.LookToAsync(ctx, target.X, target.Y, ct);

        await _roomGrain.PublishRoomEventAsync(
            new PlayerClickedAvatarEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ctx,
                PlayerId = ctx.PlayerId,
                TargetObjectId = targetObjectId,
            },
            ct
        );

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new WiredClickUserResponseMessageComposer
            {
                ObjectId = targetObjectId,
                OpenMenu = !triggers.Any(x => x.BlocksAvatarMenu),
            },
            ct
        );
    }

    /// <summary>After the stacks were rebuilt: tells the room when the trigger or the enabled achievements changed.</summary>
    private Task RefreshClickUserEnvironmentAsync(CancellationToken ct)
    {
        var hasClickUserWired = _stacksById.Values.Any(x =>
            x.Triggers.Any(t => t is WiredTriggerClickUser)
        );
        ImmutableArray<string> enabledAchievements =
        [
            .. _stacksById
                .OrderBy(x => x.Key)
                .SelectMany(x => x.Value.Addons.OfType<WiredAddonAchievementEnabler>())
                .SelectMany(x => x.EnabledAchievements)
                .Distinct(StringComparer.Ordinal),
        ];

        if (
            hasClickUserWired == _hasClickUserWired
            && enabledAchievements.SequenceEqual(_enabledAchievements)
        )
            return Task.CompletedTask;

        _hasClickUserWired = hasClickUserWired;
        _enabledAchievements = enabledAchievements;

        return _roomGrain.SendComposerToRoomAsync(BuildEnvironment(), ct);
    }

    /// <summary>A player who walks in learns what the players already here were told.</summary>
    private Task SendClickUserEnvironmentAsync(PlayerId playerId, CancellationToken ct) =>
        _hasClickUserWired || _enabledAchievements.Length > 0
            ? _roomGrain._grainFactory.SendComposerToPlayerAsync(playerId, BuildEnvironment(), ct)
            : Task.CompletedTask;

    private WiredEnvironmentMessageComposer BuildEnvironment() =>
        new()
        {
            HasClickUserWired = _hasClickUserWired,
            EnabledAchievements = _enabledAchievements,
        };
}
