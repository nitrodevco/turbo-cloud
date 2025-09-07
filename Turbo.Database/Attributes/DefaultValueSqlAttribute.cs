using System;

namespace Turbo.Database.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class DefaultValueSqlAttribute(string sql) : Attribute
{
    public string Sql { get; } = sql;
}
