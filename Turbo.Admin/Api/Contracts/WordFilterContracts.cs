using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A word of the hotel's filter, and when it was added.</summary>
public sealed record WordFilterEntry(int Id, string Word, DateTime AddedAtUtc);

/// <summary>
/// The hotel's word filter: what a filtered word is replaced with
/// (<c>Turbo:Operations:WordFilterReplacement</c>), whether the viewer may change the list, and
/// the words in alphabetical order.
/// </summary>
public sealed record WordFilterResponse(
    string Replacement,
    bool CanManage,
    WordFilterEntry[] Words
);

/// <summary>Words to add, separated by spaces, commas or new lines.</summary>
public sealed record WordFilterAddRequest(string Words);

/// <summary>A word that was not added, and why.</summary>
public sealed record WordFilterRefusal(string Word, string Reason);

/// <summary>What an add did: the words added, those already on the list, and those refused.</summary>
public sealed record WordFilterAddResponse(
    string[] Added,
    string[] Existing,
    WordFilterRefusal[] Refused
);

/// <summary>A line to try the filter on.</summary>
public sealed record WordFilterTestRequest(string Text);

/// <summary>The line as players would see it, and whether anything in it was filtered.</summary>
public sealed record WordFilterTestResponse(string Shown, bool Filtered);
