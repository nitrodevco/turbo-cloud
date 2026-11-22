using Microsoft.Data.Sqlite;
using Turbo.Import.Source;

namespace Turbo.Tests.Import;

/// <summary>
/// A Morningstar database in memory: the tables and columns an import reads, as Morningstar 3.5
/// names them (<c>ms-schema</c> of the importer's notes), with SQLite's loose types standing in for
/// MySQL's. Rows go in as anonymous objects, a property a column.
/// </summary>
internal sealed class MorningstarDb : IDisposable
{
    private const string SCHEMA = """
        CREATE TABLE users (id INTEGER PRIMARY KEY, username TEXT, password TEXT, mail TEXT, motto TEXT, look TEXT,
            gender TEXT, rank INTEGER, credits INTEGER, account_created INTEGER, last_online INTEGER, home_room INTEGER,
            online TEXT, auth_ticket TEXT);
        CREATE TABLE users_settings (user_id INTEGER, respects_received INTEGER, daily_respect_points INTEGER,
            daily_pet_respect_points INTEGER, guild_id INTEGER, home_room INTEGER, volume_system INTEGER,
            volume_furni INTEGER, volume_trax INTEGER, block_following TEXT, block_friendrequests TEXT,
            block_roominvites TEXT, block_camera_follow TEXT, old_chat TEXT, chat_color INTEGER, ui_flags INTEGER,
            club_expire_timestamp INTEGER, online_time INTEGER);
        CREATE TABLE users_currency (user_id INTEGER, type INTEGER, amount INTEGER);
        CREATE TABLE users_subscriptions (id INTEGER PRIMARY KEY, user_id INTEGER, subscription_type TEXT,
            timestamp_start INTEGER, duration INTEGER, active INTEGER);
        CREATE TABLE users_badges (id INTEGER PRIMARY KEY, user_id INTEGER, slot_id INTEGER, badge_code TEXT);
        CREATE TABLE users_wardrobe (user_id INTEGER, slot_id INTEGER, look TEXT, gender TEXT);
        CREATE TABLE users_effects (user_id INTEGER, effect INTEGER, duration INTEGER, activation_timestamp INTEGER,
            total INTEGER);
        CREATE TABLE catalog_clothing (id INTEGER PRIMARY KEY, name TEXT, setid TEXT);
        CREATE TABLE users_clothing (user_id INTEGER, clothing_id INTEGER);
        CREATE TABLE users_ignored (user_id INTEGER, target_id INTEGER);
        CREATE TABLE users_saved_searches (id INTEGER PRIMARY KEY, search_code TEXT, filter TEXT, user_id INTEGER);
        CREATE TABLE users_favorite_rooms (user_id INTEGER, room_id INTEGER);
        CREATE TABLE permissions (id INTEGER PRIMARY KEY, rank_name TEXT, level INTEGER);
        CREATE TABLE messenger_categories (id INTEGER PRIMARY KEY, name TEXT, user_id INTEGER);
        CREATE TABLE messenger_friendships (id INTEGER PRIMARY KEY, user_one_id INTEGER, user_two_id INTEGER,
            relation INTEGER, friends_since INTEGER, category INTEGER);
        CREATE TABLE messenger_friendrequests (user_to_id INTEGER, user_from_id INTEGER);
        CREATE TABLE navigator_flatcats (id INTEGER PRIMARY KEY, min_rank INTEGER, caption_save TEXT, caption TEXT,
            public TEXT, order_num INTEGER);
        CREATE TABLE room_models (name TEXT, door_x INTEGER, door_y INTEGER, door_dir INTEGER, heightmap TEXT);
        CREATE TABLE room_models_custom (id INTEGER PRIMARY KEY, name TEXT, door_x INTEGER, door_y INTEGER,
            door_dir INTEGER, heightmap TEXT);
        CREATE TABLE rooms (id INTEGER PRIMARY KEY, owner_id INTEGER, owner_name TEXT, name TEXT, description TEXT,
            model TEXT, password TEXT, state TEXT, users_max INTEGER, category INTEGER, score INTEGER,
            paper_floor TEXT, paper_wall TEXT, paper_landscape TEXT, thickness_wall INTEGER, wall_height INTEGER,
            thickness_floor INTEGER, moodlight_data TEXT, tags TEXT, is_public TEXT, is_staff_picked TEXT,
            allow_other_pets TEXT, allow_other_pets_eat TEXT, allow_walkthrough TEXT, allow_hidewall TEXT,
            chat_protection INTEGER, who_can_mute INTEGER, who_can_kick INTEGER, who_can_ban INTEGER,
            guild_id INTEGER, override_model TEXT, trade_mode INTEGER);
        CREATE TABLE room_rights (room_id INTEGER, user_id INTEGER);
        CREATE TABLE room_bans (room_id INTEGER, user_id INTEGER, ends INTEGER);
        CREATE TABLE room_votes (user_id INTEGER, room_id INTEGER);
        CREATE TABLE room_wordfilter (room_id INTEGER, word TEXT);
        CREATE TABLE guilds (id INTEGER PRIMARY KEY, user_id INTEGER, name TEXT, description TEXT, room_id INTEGER,
            state INTEGER, rights TEXT, color_one INTEGER, color_two INTEGER, badge TEXT, date_created INTEGER,
            forum TEXT, read_forum TEXT, post_messages TEXT, post_threads TEXT, mod_forum TEXT);
        CREATE TABLE guilds_members (id INTEGER PRIMARY KEY, guild_id INTEGER, user_id INTEGER, level_id INTEGER,
            member_since INTEGER);
        CREATE TABLE guilds_forums_threads (id INTEGER PRIMARY KEY, guild_id INTEGER, opener_id INTEGER,
            subject TEXT, posts_count INTEGER, created_at INTEGER, updated_at INTEGER, state INTEGER,
            pinned INTEGER, locked INTEGER, admin_id INTEGER);
        CREATE TABLE guilds_forums_comments (id INTEGER PRIMARY KEY, thread_id INTEGER, user_id INTEGER,
            message TEXT, created_at INTEGER, state INTEGER, admin_id INTEGER);
        CREATE TABLE items_base (id INTEGER PRIMARY KEY, sprite_id INTEGER, item_name TEXT, public_name TEXT,
            width INTEGER, length INTEGER, stack_height REAL, allow_stack TEXT, allow_sit TEXT, allow_lay TEXT,
            allow_walk TEXT, allow_gift TEXT, allow_trade TEXT, allow_recycle TEXT, allow_marketplace_sell TEXT,
            allow_inventory_stack TEXT, type TEXT, interaction_type TEXT, interaction_modes_count INTEGER,
            vending_ids TEXT, multiheight TEXT, customparams TEXT, effect_id_male INTEGER, effect_id_female INTEGER,
            clothing_on_walk TEXT);
        CREATE TABLE items (id INTEGER PRIMARY KEY, user_id INTEGER, room_id INTEGER, item_id INTEGER, wall_pos TEXT,
            x INTEGER, y INTEGER, z REAL, rot INTEGER, extra_data TEXT, wired_data TEXT, limited_data TEXT,
            guild_id INTEGER);
        CREATE TABLE items_teleports (teleport_one_id INTEGER, teleport_two_id INTEGER);
        CREATE TABLE items_crackable (item_id INTEGER PRIMARY KEY, count INTEGER, prizes TEXT, required_effect INTEGER);
        CREATE TABLE trax_playlist (trax_item_id INTEGER, item_id INTEGER);
        CREATE TABLE soundtracks (id INTEGER PRIMARY KEY, name TEXT, author TEXT, code TEXT, track TEXT, length INTEGER);
        CREATE TABLE catalog_pages (id INTEGER PRIMARY KEY, parent_id INTEGER, caption_save TEXT, caption TEXT,
            icon_color INTEGER, icon_image INTEGER, visible TEXT, enabled TEXT, min_rank INTEGER, club_only TEXT,
            order_num INTEGER, page_layout TEXT, page_headline TEXT, page_teaser TEXT, page_special TEXT,
            page_text1 TEXT, page_text2 TEXT, page_text_details TEXT, page_text_teaser TEXT);
        CREATE TABLE catalog_items (id INTEGER PRIMARY KEY, item_ids TEXT, page_id INTEGER, catalog_name TEXT,
            cost_credits INTEGER, cost_points INTEGER, points_type INTEGER, amount INTEGER, limited_stack INTEGER,
            limited_sells INTEGER, order_number INTEGER, offer_id INTEGER, extradata TEXT, have_offer TEXT,
            club_only TEXT);
        CREATE TABLE chatlogs_room (room_id INTEGER, user_from_id INTEGER, user_to_id INTEGER, message TEXT,
            timestamp INTEGER);
        CREATE TABLE chatlogs_private (user_from_id INTEGER, user_to_id INTEGER, message TEXT, timestamp INTEGER);
        CREATE TABLE marketplace_items (id INTEGER PRIMARY KEY, item_id INTEGER, user_id INTEGER, price INTEGER,
            timestamp INTEGER, sold_timestamp INTEGER, state INTEGER);
        CREATE TABLE users_pets (id INTEGER PRIMARY KEY, user_id INTEGER, room_id INTEGER, name TEXT, race INTEGER,
            type INTEGER, color TEXT, happiness INTEGER, experience INTEGER, energy INTEGER, hunger INTEGER,
            thirst INTEGER, respect INTEGER, created INTEGER, x INTEGER, y INTEGER, z REAL, rot INTEGER,
            hair_style INTEGER, hair_color INTEGER, saddle TEXT, ride TEXT, mp_type INTEGER, mp_color INTEGER,
            mp_nose INTEGER, mp_nose_color INTEGER, mp_eyes INTEGER, mp_eyes_color INTEGER, mp_mouth INTEGER,
            mp_mouth_color INTEGER, mp_death_timestamp INTEGER, mp_breedable TEXT, mp_allow_breed TEXT,
            mp_is_dead INTEGER, saddle_item_id INTEGER);
        CREATE TABLE bots (id INTEGER PRIMARY KEY, user_id INTEGER, room_id INTEGER, name TEXT, motto TEXT,
            figure TEXT, gender TEXT, x INTEGER, y INTEGER, z REAL, rot INTEGER, chat_lines TEXT, chat_auto TEXT,
            chat_random TEXT, chat_delay INTEGER, dance INTEGER, freeroam TEXT, type TEXT, effect INTEGER,
            bubble_id INTEGER);
        CREATE TABLE bans (id INTEGER PRIMARY KEY, user_id INTEGER, ip TEXT, machine_id TEXT, user_staff_id INTEGER,
            timestamp INTEGER, ban_expire INTEGER, ban_reason TEXT, type TEXT, cfh_topic INTEGER);
        """;

    private readonly SqliteConnection _conn = new("DataSource=:memory:");

    public MorningstarDb()
    {
        _conn.Open();

        using var cmd = _conn.CreateCommand();

        cmd.CommandText = SCHEMA;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Adds a row: each property of <paramref name="row"/> is a column of that name.</summary>
    public MorningstarDb Insert(string table, object row)
    {
        var properties = row.GetType().GetProperties();

        using var cmd = _conn.CreateCommand();

        cmd.CommandText =
            $"INSERT INTO {table} ({string.Join(", ", properties.Select(p => p.Name))}) "
            + $"VALUES ({string.Join(", ", properties.Select(p => "@" + p.Name))})";

        foreach (var property in properties)
            cmd.Parameters.AddWithValue(
                "@" + property.Name,
                property.GetValue(row) ?? DBNull.Value
            );

        cmd.ExecuteNonQuery();

        return this;
    }

    /// <summary>Runs a statement against the database, to change a row a fixture made.</summary>
    public MorningstarDb Execute(string sql)
    {
        using var cmd = _conn.CreateCommand();

        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        return this;
    }

    public Task<SourceDatabase> OpenAsync(CancellationToken ct = default) =>
        SourceDatabase.OpenAsync(_conn, ct);

    public void Dispose() => _conn.Dispose();
}
