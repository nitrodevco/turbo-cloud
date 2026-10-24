"""Split Turbo.Tests into CI shards by namespace; the last shard takes every test not listed."""

import os
import sys

# One list of name prefixes per shard, balanced by the test durations CI measured (the shards'
# TRX files). A test belongs to the shard with the longest prefix it starts with; a test no
# prefix matches runs in the last shard, so a new namespace is never left out.
SHARDS = (
    ("Turbo.Tests.Rooms.Wired",),
    ("Turbo.Tests.Rooms.",),
    ("Turbo.Tests.PacketHandlers.", "Turbo.Tests.Commands.", "Turbo.Tests.Catalog."),
    (),
)


def _prefixes():
    return [(index, prefix) for index, shard in enumerate(SHARDS) for prefix in shard]


def test_filter(index):
    """The dotnet test --filter expression of shard index (0-based)."""
    if not 0 <= index < len(SHARDS):
        raise ValueError(f"shard {index} is not one of the {len(SHARDS)} shards")

    listed = _prefixes()
    if index == len(SHARDS) - 1 and not SHARDS[index]:
        return "&".join(f"FullyQualifiedName!~{prefix}" for _, prefix in listed)

    terms = []
    for prefix in SHARDS[index]:
        # A longer prefix of another shard inside this one is that shard's.
        excluded = [
            other for owner, other in listed
            if owner != index and other != prefix and other.startswith(prefix)
        ]
        terms.append("&".join([f"FullyQualifiedName~{prefix}"]
                              + [f"FullyQualifiedName!~{other}" for other in excluded]))
    return "|".join(f"({term})" for term in terms)


def shard_of(name):
    """The shard a fully qualified test name runs in, as the filters select it."""
    matches = [(len(prefix), index) for index, prefix in _prefixes() if name.startswith(prefix)]
    return max(matches)[1] if matches else len(SHARDS) - 1


if __name__ == "__main__":
    # ci_test_shards.py <1-based shard>: write filter=<expression> to GITHUB_OUTPUT.
    expression = test_filter(int(sys.argv[1]) - 1)
    print(expression)
    if output := os.environ.get("GITHUB_OUTPUT"):
        with open(output, "a", encoding="utf-8") as stream:
            stream.write(f"filter={expression}\n")
