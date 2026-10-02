using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Guilds.Grains;

/// <summary>
/// The group chat: the messenger conversation whose id is minus the group's
/// (<c>MainView.startConversation</c> draws it with the group badge). Lines are not stored;
/// they go to the members listening now, which is how the client treats a group chat too
/// (it asks no history for one).
/// </summary>
internal sealed partial class GuildGrain
{
    public Task<bool> JoinChatAsync(PlayerId playerId, CancellationToken ct)
    {
        if (!GuildMemberRanks.IsMember(GetRank(playerId)))
            return Task.FromResult(false);

        _state.ChatListenerIds.Add(playerId.Value);

        EnsureChatListenerTimer();

        return Task.FromResult(true);
    }

    public Task LeaveChatAsync(PlayerId playerId, CancellationToken ct)
    {
        StopListening(playerId);

        return Task.CompletedTask;
    }

    public async Task<bool> SendChatMessageAsync(
        PlayerId senderId,
        string senderName,
        string senderFigure,
        string message,
        string messageId,
        CancellationToken ct
    )
    {
        if (_state.Guild is null || !GuildMemberRanks.IsMember(GetRank(senderId)))
            return false;

        var recipients = _state
            .ChatListenerIds.Where(id => id != senderId.Value)
            .Select(PlayerId.Parse)
            .ToList();

        if (recipients.Count == 0)
            return true;

        // One composer for every recipient, serialized once: the sender's own copy, which
        // confirms their pending bubble, is their messenger's to send.
        await _grainFactory.SendComposerToPlayersAsync(
            recipients,
            new NewConsoleMessageMessageComposer
            {
                ChatId = -GuildId.Value,
                Message = message,
                SecondsSinceSent = 0,
                MessageId = messageId,
                ConfirmationId = 0,
                SenderId = senderId,
                SenderName = senderName,
                SenderFigure = senderFigure,
            },
            ct
        );

        return true;
    }

    /// <summary>
    /// Drops a player from the chat, and stops the listener check once nobody listens. Called
    /// when they leave the chat and when they stop being a member, so a kicked player hears
    /// nothing more even before their messenger catches up.
    /// </summary>
    private void StopListening(PlayerId playerId)
    {
        if (!_state.ChatListenerIds.Remove(playerId.Value))
            return;

        if (_state.ChatListenerIds.Count > 0)
            return;

        _chatListenerTimer?.Dispose();
        _chatListenerTimer = null;
    }

    /// <summary>
    /// Starts the listener check with the first listener. Its ticks keep the grain loaded
    /// (<c>KeepAlive</c>) while anyone listens: the listener set is memory only, and a group
    /// collected for idleness would silently stop delivering its chat.
    /// </summary>
    private void EnsureChatListenerTimer()
    {
        if (_chatListenerTimer is not null)
            return;

        _chatListenerTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((GuildGrain)self!).CheckChatListenersAsync(ct),
            this,
            new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.FromMilliseconds(_guildConfig.ChatListenerCheckMs),
                Period = TimeSpan.FromMilliseconds(_guildConfig.ChatListenerCheckMs),
                KeepAlive = true,
            }
        );
    }

    /// <summary>
    /// Drops listeners with no session: a messenger leaves on its owner going offline, but a
    /// silo that stopped abruptly never told anyone. A failed check is logged and the schedule
    /// goes on.
    /// </summary>
    private async Task CheckChatListenersAsync(CancellationToken ct)
    {
        try
        {
            var listenerIds = _state.ChatListenerIds.Select(PlayerId.Parse).ToList();
            var online = await Task.WhenAll(
                listenerIds.Select(playerId =>
                    _grainFactory.GetPlayerPresenceGrain(playerId).HasActiveSessionAsync(ct)
                )
            );

            for (var i = 0; i < listenerIds.Count; i++)
            {
                if (!online[i])
                    StopListening(listenerIds[i]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check the chat listeners of guild {GuildId}",
                GuildId.Value
            );
        }
    }
}
