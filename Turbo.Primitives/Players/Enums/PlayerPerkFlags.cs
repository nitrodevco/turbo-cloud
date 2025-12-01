using System;

namespace Turbo.Primitives.Players.Enums;

[Flags]
public enum PlayerPerkFlags
{
    None = 0,
    Citizen = 1 << 0,
    VoteInCompetitions = 1 << 1,
    Trade = 1 << 2,
    CallOnHelpers = 1 << 3,
    JudgeChatReviews = 1 << 4,
    NavigatorRoomThumbnailCamera = 1 << 5,
    UseGuideTool = 1 << 6,
    MouseZoom = 1 << 7,
    HabboClubOfferBeta = 1 << 8,
    NavigatorPhaseTwo2014 = 1 << 9,
    UnityTrade = 1 << 10,
    BuilderAtWork = 1 << 11,
    Camera = 1 << 12,
}

public static class PlayerPerkExtensions
{
    public static string ToLegacyString(PlayerPerkFlags perk) =>
        perk switch
        {
            PlayerPerkFlags.Citizen => "CITIZEN",
            PlayerPerkFlags.VoteInCompetitions => "VOTE_IN_COMPETITIONS",
            PlayerPerkFlags.Trade => "TRADE",
            PlayerPerkFlags.CallOnHelpers => "CALL_ON_HELPERS",
            PlayerPerkFlags.JudgeChatReviews => "JUDGE_CHAT_REVIEWS",
            PlayerPerkFlags.NavigatorRoomThumbnailCamera => "NAVIGATOR_ROOM_THUMBNAIL_CAMERA",
            PlayerPerkFlags.UseGuideTool => "USE_GUIDE_TOOL",
            PlayerPerkFlags.MouseZoom => "MOUSE_ZOOM",
            PlayerPerkFlags.HabboClubOfferBeta => "HABBO_CLUB_OFFER_BETA",
            PlayerPerkFlags.NavigatorPhaseTwo2014 => "NAVIGATOR_PHASE_TWO_2014",
            PlayerPerkFlags.UnityTrade => "UNITY_TRADE",
            PlayerPerkFlags.BuilderAtWork => "BUILDER_AT_WORK",
            PlayerPerkFlags.Camera => "CAMERA",
            _ => throw new ArgumentOutOfRangeException(nameof(perk), perk, null),
        };

    public static PlayerPerkFlags? FromLegacyString(string perkString) =>
        perkString switch
        {
            "CITIZEN" => PlayerPerkFlags.Citizen,
            "VOTE_IN_COMPETITIONS" => PlayerPerkFlags.VoteInCompetitions,
            "TRADE" => PlayerPerkFlags.Trade,
            "CALL_ON_HELPERS" => PlayerPerkFlags.CallOnHelpers,
            "JUDGE_CHAT_REVIEWS" => PlayerPerkFlags.JudgeChatReviews,
            "NAVIGATOR_ROOM_THUMBNAIL_CAMERA" => PlayerPerkFlags.NavigatorRoomThumbnailCamera,
            "USE_GUIDE_TOOL" => PlayerPerkFlags.UseGuideTool,
            "MOUSE_ZOOM" => PlayerPerkFlags.MouseZoom,
            "HABBO_CLUB_OFFER_BETA" => PlayerPerkFlags.HabboClubOfferBeta,
            "NAVIGATOR_PHASE_TWO_2014" => PlayerPerkFlags.NavigatorPhaseTwo2014,
            "UNITY_TRADE" => PlayerPerkFlags.UnityTrade,
            "BUILDER_AT_WORK" => PlayerPerkFlags.BuilderAtWork,
            "CAMERA" => PlayerPerkFlags.Camera,
            _ => null,
        };
}
