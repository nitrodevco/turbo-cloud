using System;

namespace Turbo.Events.Exceptions;

public class EventAbortedException(string? reason = null) : Exception(reason) { }
