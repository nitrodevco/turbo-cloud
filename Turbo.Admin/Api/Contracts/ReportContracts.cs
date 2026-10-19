using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A call for help as the panel lists it. <see cref="State"/> is <c>open</c>, <c>picked</c> (a
/// moderator holds it) or <c>closed</c>; <see cref="Source"/> is where it was sent from:
/// <c>room</c>, <c>im</c>, <c>photo</c> or <c>forum</c>. <see cref="Topic"/> and
/// <see cref="Category"/> are the client's texts for them, else their names. Names are null when
/// the player or room is gone.
/// </summary>
public sealed record ReportItem(
    int Id,
    DateTime AtUtc,
    string State,
    string Source,
    int TopicId,
    string Topic,
    string Category,
    int ReporterId,
    string? ReporterName,
    int? ReportedId,
    string? ReportedName,
    int? RoomId,
    string? RoomName,
    int? PickerId,
    string? PickerName,
    string Message,
    DateTime? ClosedAtUtc,
    string? CloseReason,
    bool Sanctioned
);

/// <summary>
/// A run of reports, newest first, paged by id as the chat log is; with how many are open and how
/// many of those a moderator holds, whatever the filter.
/// </summary>
public sealed record ReportListResponse(
    int PageSize,
    bool HasOlder,
    bool HasNewer,
    int Open,
    int Picked,
    ReportItem[] Entries
);

/// <summary>A line sent with a report; <see cref="Reported"/> when the reported player said it.</summary>
public sealed record ReportChatLine(
    DateTime AtUtc,
    int PlayerId,
    string? PlayerName,
    string Text,
    bool Reported
);

/// <summary>
/// A player's reports so far: <see cref="Total"/> about them (or sent by them) and of those how
/// many were closed with a sanction, as abusive and as useless.
/// </summary>
public sealed record ReportHistory(int Total, int Sanctioned, int Abusive, int Useless);

/// <summary>
/// The sanction a report about the player closed with one would give now: the next step of the
/// hotel's ladder (<c>Turbo:Operations:CfhDefaultSanctions</c>). <see cref="Name"/> is
/// <c>ALERT</c>, <c>MUTE</c>, <c>BAN</c> or <c>BAN_PERMANENT</c>.
/// </summary>
public sealed record ReportSanction(string Name, int LengthHours);

/// <summary>
/// One report with what came with it: the chat lines, what an unlawful activity report gave, the
/// photo or forum post it is about, its appeal (<c>none</c>, <c>appealed</c>, <c>upheld</c>,
/// <c>dismissed</c>), both players' histories, the sanction closing it with one would give, and
/// the other open reports about the same player.
/// </summary>
public sealed record ReportDetailResponse(
    ReportItem Report,
    ReportChatLine[] Lines,
    string? GivenName,
    string? GivenEmail,
    string? PhotoId,
    int? ItemId,
    int? GroupId,
    string? GroupName,
    int? ForumThreadId,
    int? ForumMessageId,
    string Appeal,
    ReportHistory? AboutReported,
    ReportHistory BySender,
    ReportSanction? DefaultSanction,
    ReportItem[] Related
);

/// <summary>
/// Closes a report: <see cref="Reason"/> is <c>resolved</c>, <c>useless</c> or <c>abusive</c>.
/// <see cref="Sanction"/> (resolved only) gives the reported player the default sanction too;
/// <see cref="WithRelated"/> closes the other open reports about the same player along with it.
/// </summary>
public sealed record CloseReportRequest(string Reason, bool Sanction, bool WithRelated);
