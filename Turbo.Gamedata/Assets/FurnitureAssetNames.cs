using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// The furniture assets the client loads under a name furnidata does not list, as nitro-studio's
/// converter lists them (<c>GetAllFurniture.ts</c>, <c>GetPosterIds.ts</c>):
/// <list type="bullet">
///   <item>
///     Posters. Furnidata has one <c>poster</c> item; each placed poster carries its id, and the
///     client loads <c>poster</c> + id (<c>poster5</c>) at the <c>poster</c> item's revision. There
///     is no <c>poster.swf</c>. Habbo lists no ids, but each poster has a <c>poster_&lt;id&gt;_name</c>
///     text (an id with a text may still have no file).
///   </item>
///   <item>
///     Ad campaigns' versions of a furniture, loaded under a name of their own at its revision.
///   </item>
/// </list>
/// </summary>
internal static partial class FurnitureAssetNames
{
    /// <summary>The one furnidata item every poster is, which has no file of its own.</summary>
    public const string POSTER = "poster";

    /// <summary>The campaign versions: each alias, by the furniture it stands in for.</summary>
    public static readonly ImmutableArray<(string Source, string Alias)> ALIASES =
    [
        ("footylamp", "footylamp_campaign_ing"),
        ("easy_bowl2", "easy_bowl"),
        ("ads_cllava2", "ads_cllava"),
        ("rare_icecream_campaign", "rare_icecream_campaign2"),
        ("calippo", "calippo_cmp"),
        ("igor_seat", "igor_seatcmp"),
        ("ads_711", "ads_711c"),
        ("ads_cltele", "ads_cltele_cmp"),
        ("ads_ob_pillow", "ads_ob_pillowcmp"),
        ("ads_711shelf", "ads_711shelfcmp"),
        ("ads_frankb", "ads_frankbcmp"),
        ("ads_grefusa_cactus", "ads_grefusa_cactus_camp"),
        ("ads_cl_jukeb", "ads_cl_jukeb_camp"),
        ("ads_reebok_block2", "ads_reebok_block2cmp"),
        ("ads_cl_sofa", "ads_cl_sofa_cmp"),
        ("ads_calip_cola", "ads_calip_colac"),
        ("ads_calip_chair", "ads_calip_chaircmp"),
        ("ads_calip_pool", "ads_calip_pool_cmp"),
        ("ads_calip_tele", "ads_calip_telecmp"),
        ("ads_calip_parasol", "ads_calip_parasol_cmp"),
        ("ads_calip_lava", "ads_calip_lava2"),
        ("ads_calip_fan", "ads_calip_fan_cmp"),
        ("ads_oc_soda", "ads_oc_soda_cmp"),
        ("ads_1800tele", "ads_1800tele_cmp"),
        ("ads_spang_sleep", "ads_spang_sleep_cmp"),
        ("ads_cl_moodi", "ads_cl_moodi_camp"),
        ("ads_droetker_paula", "ads_droetker_paula_cmp"),
        ("ads_chups", "ads_chups_camp"),
        ("garden_seed", "garden_seed_cmp"),
        ("ads_grefusa_yum", "ads_grefusa_yum_camp"),
        ("ads_cheetos", "ads_cheetos_camp"),
        ("ads_chocapic", "ads_chocapic_camp"),
        ("ads_capri_chair", "ads_capri_chair_camp"),
        ("ads_capri_lava", "ads_capri_lava_camp"),
        ("ads_capri_arcade", "ads_capri_arcade_camp"),
        ("ads_pepsi0", "ads_pepsi0_camp"),
        ("ads_cheetos_hotdog", "ads_cheetos_hotdog_camp"),
        ("ads_cheetos_bath", "ads_cheetos_bath_camp"),
        ("ads_oc_soda_cherry", "ads_oc_soda_cherry_cmp"),
        ("ads_disney_tv", "ads_disney_tvcmp"),
        ("ads_hh_safe", "ads_hh_safecmp"),
        ("ads_sunnyvend", "ads_sunnyvend_camp"),
        ("ads_rangocactus", "ads_rangocactus_camp"),
        ("ads_wowpball", "ads_wowpball_camp"),
        ("ads_suun", "ads_suun_camp"),
        ("ads_liisu", "ads_liisu_camp"),
        ("ads_honeymonster", "ads_honeymonster_cmp"),
        ("ads_ag_crate", "ads_ag_crate_camp"),
        ("ads_dfrisss", "ads_dfrisss_camp"),
    ];

    private static readonly Dictionary<string, string> SourceByAlias = ALIASES.ToDictionary(
        x => x.Alias,
        x => x.Source,
        StringComparer.Ordinal
    );

    /// <summary>Whether a bundle is one poster's (<c>poster5</c>).</summary>
    public static bool IsPoster(string name) => PosterPattern().IsMatch(name);

    /// <summary>
    /// The furnidata item a bundle stands in for: <c>poster</c> for a poster, the furniture an
    /// alias is a campaign version of; null for a bundle named as its own item.
    /// </summary>
    public static string? SourceOf(string name) =>
        IsPoster(name) ? POSTER : SourceByAlias.GetValueOrDefault(name);

    /// <summary>
    /// Every poster id Habbo's external texts name (<c>poster_&lt;id&gt;_name</c> or
    /// <c>_desc</c>), in order.
    /// </summary>
    public static ImmutableArray<int> PosterIds(byte[] externalTexts)
    {
        var ids = new SortedSet<int>();

        foreach (var line in Encoding.UTF8.GetString(externalTexts).Split('\n'))
        {
            var match = PosterTextPattern().Match(line);

            if (match.Success && int.TryParse(match.Groups[1].ValueSpan, out var id))
                ids.Add(id);
        }

        return [.. ids];
    }

    [GeneratedRegex(@"^poster\d+$")]
    private static partial Regex PosterPattern();

    [GeneratedRegex(@"^poster_(\d+)_(?:name|desc)=")]
    private static partial Regex PosterTextPattern();
}
