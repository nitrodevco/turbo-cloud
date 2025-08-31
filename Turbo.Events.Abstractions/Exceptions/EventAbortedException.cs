using System;

namespace Turbo.Events.Abstractions.Exceptions;

public class EventAbortedException(string? reason = null) : Exception(reason) { }
