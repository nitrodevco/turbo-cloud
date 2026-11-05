namespace Turbo.Database.Backups;

/// <summary>
/// A table of the database as <see cref="DatabaseDump.TablesAsync"/> finds it: its name, how many
/// rows it holds (MySQL's own estimate when <paramref name="RowsEstimated"/>) and the room its
/// data and indexes take, when the database says.
/// </summary>
public sealed record DatabaseTableInfo(string Name, long Rows, bool RowsEstimated, long? Bytes);
