-- Fresh start: removes every player and everything they own or left behind (rooms, furniture,
-- pets, bots, guilds, friends, messages, badges, wallets, achievements, sanctions, logs, sign-ins),
-- and resets the ids. Keeps the hotel's setup: the catalog, furniture and badge definitions,
-- currency types, permission groups, room models, navigator categories, pet breeds, chat styles,
-- guild badge parts and colours, and the migration history.
--
-- Stop the server first: grains hold player and room state in memory and would write it back.
-- Back up first: there is no undo. Works on MariaDB and MySQL 8.
--
-- Afterwards no one has panel access. If the owner is named (TURBO_OWNER_NAME or
-- TURBO_OWNER_DISCORD_ID), they become the owner again when they sign up and get their setup link
-- in the server log. Otherwise: make a player, give them their groups, and print a setup
-- link from the server console (adminsetup <name>).

SET FOREIGN_KEY_CHECKS = 0;

-- Rooms and what is in them
TRUNCATE TABLE room_bans;
TRUNCATE TABLE room_chatlogs;
TRUNCATE TABLE room_entry_logs;
TRUNCATE TABLE room_events;
TRUNCATE TABLE room_filter_words;
TRUNCATE TABLE room_mutes;
TRUNCATE TABLE room_ratings;
TRUNCATE TABLE room_rights;
TRUNCATE TABLE furniture;
TRUNCATE TABLE builders_club_furniture;
TRUNCATE TABLE bots;
TRUNCATE TABLE pets;
TRUNCATE TABLE pet_nutrition_operations;
TRUNCATE TABLE pet_respect_operations;
TRUNCATE TABLE guild_members;
TRUNCATE TABLE guilds;
TRUNCATE TABLE rooms;

-- Friends and messages
TRUNCATE TABLE messenger_blocked;
TRUNCATE TABLE messenger_categories;
TRUNCATE TABLE messenger_friends;
TRUNCATE TABLE messenger_ignored;
TRUNCATE TABLE messenger_messages;
TRUNCATE TABLE messenger_requests;

-- Achievements and respect
TRUNCATE TABLE achievement_audit;
TRUNCATE TABLE achievement_distinct_values;
TRUNCATE TABLE achievement_facts;
TRUNCATE TABLE achievement_progress;
TRUNCATE TABLE achievement_projections;
TRUNCATE TABLE achievement_wallet_receipts;
TRUNCATE TABLE human_respect_operations;
TRUNCATE TABLE human_respect_participant_receipts;

-- The players and everything of theirs
TRUNCATE TABLE player_badges;
TRUNCATE TABLE player_bonus_rare_progress;
TRUNCATE TABLE player_chat_styles_owned;
TRUNCATE TABLE player_club_gifts;
TRUNCATE TABLE player_currencies;
TRUNCATE TABLE player_discord_links;
TRUNCATE TABLE player_favorite_rooms;
TRUNCATE TABLE player_navigator_collapsed_categories;
TRUNCATE TABLE player_navigator_saved_searches;
TRUNCATE TABLE player_navigator_view_modes;
TRUNCATE TABLE player_outfits;
TRUNCATE TABLE player_permission_groups;
TRUNCATE TABLE player_permission_meta;
TRUNCATE TABLE player_permission_nodes;
TRUNCATE TABLE player_sanctions;
TRUNCATE TABLE player_settings;
TRUNCATE TABLE player_subscriptions;
TRUNCATE TABLE player_unseen_items;
TRUNCATE TABLE ltd_raffle_entries;
TRUNCATE TABLE security_tickets;
TRUNCATE TABLE web_sessions;
TRUNCATE TABLE admin_passkeys;
TRUNCATE TABLE players;

-- Logs and audits about players
TRUNCATE TABLE command_logs;
TRUNCATE TABLE permission_audit;
TRUNCATE TABLE performance_logs;

SET FOREIGN_KEY_CHECKS = 1;

-- Limited editions were sold to players who are gone: every series is unsold again.
UPDATE ltd_series SET remaining_quantity = total_quantity, has_raffle_finished = 0;
