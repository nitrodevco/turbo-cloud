# The reception's timed content never updates (timing-code and countdown requests get no reply)

The hotel reception view asks the server two timing questions, and Turbo answers neither
(the handlers are stubs), so scheduled reception content and countdowns never show.

1. **Current timing code** (`GetCurrentTimingCode`). The client sends one string: a schedule
   of `;`-separated entries, each `yyyy-MM-dd HH:mm,<code>` (seconds may also appear:
   `yyyy-MM-dd HH:mm:ss`). The reply (`CurrentTimingCode`) must echo the exact schedule string
   the client sent (the client matches replies by it), followed by the code of the entry with
   the latest start time that is not in the future, or an empty string when none has started.
   Malformed entries are ignored.

2. **Seconds until** (`GetSecondsUntil`). The client sends one string, a time in the same
   `yyyy-MM-dd HH:mm[:ss]` format. The reply (`SecondsUntil`) must echo that exact string,
   followed by an int: the whole number of seconds from now until that time, or 0 if it has
   passed or cannot be parsed.

All times in these strings are UTC, resolved against the server's current UTC time. The wire
order is: string, then string (timing code) / string, then int (seconds until). The incoming
seconds-until message currently does not even read its string.
