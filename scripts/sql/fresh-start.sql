-- Fresh start: removes every player and everything they own or left behind (rooms, furniture,
-- pets, bots, guilds, friends, messages, badges, wallets and their ledger, achievements, trades,
-- sanctions, logs, sign-ins),
-- and resets the ids. Keeps the hotel's setup: the catalog, furniture and badge definitions,
-- currency types, permission groups, room models, navigator categories, pet breeds, chat styles,
-- guild badge parts and colours, and the migration history.
--
-- Stop the server first: grains hold player and room state in memory and would write it back.
-- Back up first: there is no undo. PostgreSQL. One statement, so it all happens or none of it does;
-- PostgreSQL refuses it, and changes nothing, if a table added since still points at a player.
--
-- Afterwards no one has panel access. If the owner is named (TURBO_OWNER_NAME or
-- TURBO_OWNER_DISCORD_ID), they become the owner again when they sign up and get their setup link
-- in the server log. Otherwise: make a player, give them their groups, and print a setup
-- link from the server console (adminsetup <name>).

TRUNCATE TABLE
    -- Rooms and what is in them
    rooms,
    room_bans,
    room_chat_logs,
    room_entry_logs,
    room_events,
    room_filter_words,
    room_mutes,
    room_raid_protections,
    room_ratings,
    room_rights,
    furniture,
    furniture_builders_club,
    bots,
    pets,
    pet_feedings,
    pet_respects,
    wired_chests,
    wired_chest_transactions,
    wired_chest_transaction_entries,
    -- Groups and their forums
    guilds,
    guild_members,
    guild_forums,
    guild_forum_threads,
    guild_forum_messages,
    guild_forum_read_markers,
    -- Friends and messages
    messenger_blocks,
    messenger_categories,
    messenger_friends,
    messenger_ignores,
    messenger_messages,
    messenger_requests,
    -- Achievements and respect
    achievement_audit_logs,
    achievement_distinct_values,
    achievement_facts,
    achievement_progress,
    achievement_projections,
    achievement_wallet_receipts,
    player_respects,
    player_respect_changes,
    -- The players and everything of theirs
    players,
    player_badges,
    player_bonus_rare_progress,
    player_bound_clothing,
    player_chat_styles,
    player_club_gifts,
    player_currencies,
    player_currency_ledger,
    player_daily_tasks,
    player_effects,
    player_favorite_rooms,
    player_figure_sets,
    player_navigator_collapsed_categories,
    player_navigator_saved_searches,
    player_navigator_view_modes,
    player_outfits,
    player_permission_groups,
    player_permission_meta,
    player_permission_nodes,
    player_quests,
    player_reward_tracks,
    player_settings,
    player_subscriptions,
    player_subscription_grants,
    player_logins,
    player_unseen_items,
    player_wallet_credit_receipts,
    catalog_bonus_rare_receipts,
    catalog_ltd_entries,
    furniture_transfers,
    hotel_community_goal_contributions,
    voucher_redemptions,
    auth_admin_passkeys,
    auth_discord_links,
    auth_login_tickets,
    auth_web_sessions,
    -- Moderation, logs and audits about players
    moderation_cfh_reports,
    moderation_cfh_report_chat_lines,
    moderation_command_logs,
    moderation_sanctions,
    permission_audit_logs
RESTART IDENTITY;

-- Limited editions were sold to players who are gone: every series is unsold again.
UPDATE catalog_ltd_series SET remaining_quantity = total_quantity, has_raffle_finished = false;
