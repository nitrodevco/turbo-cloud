"""Every test runs in exactly one CI shard, the one its longest listed prefix names."""

from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import ci_test_shards


def matches(expression, name):
    """dotnet test's filter for the shapes the shards use: (a&b)|(c) over FullyQualifiedName."""
    def term(condition):
        negated = "!~" in condition
        value = condition.split("~", 1)[1]
        return (value not in name) if negated else (value in name)

    return any(
        all(term(condition) for condition in group.strip("()").split("&"))
        for group in expression.split("|")
    )


NAMES = (
    "Turbo.Tests.Rooms.WiredClockStateTests.A_timer_counts",
    "Turbo.Tests.Rooms.RoomChatTests.A_message_is_heard",
    "Turbo.Tests.PacketHandlers.CameraRendererTests.A_photo",
    "Turbo.Tests.Commands.OperatorTests.A_command",
    "Turbo.Tests.Catalog.CatalogPageTests.A_page",
    "Turbo.Tests.Networking.SessionLivenessTests.A_session",
    "Turbo.Tests.SomeNamespaceAddedLater.ATests.A_test",
)


class ShardTests(unittest.TestCase):
    def assert_each_name_in_one_shard(self):
        for name in NAMES:
            selected = [
                index for index in range(len(ci_test_shards.SHARDS))
                if matches(ci_test_shards.test_filter(index), name)
            ]
            self.assertEqual([ci_test_shards.shard_of(name)], selected, name)

    def test_each_test_runs_in_exactly_one_shard(self):
        self.assert_each_name_in_one_shard()

    def test_a_namespace_nobody_listed_runs_in_the_last_shard(self):
        self.assertEqual(
            len(ci_test_shards.SHARDS) - 1,
            ci_test_shards.shard_of("Turbo.Tests.SomeNamespaceAddedLater.ATests.A_test"),
        )

    def test_nested_prefixes_across_shards_still_select_each_test_once(self):
        shards = (
            ("Turbo.Tests.Rooms.",),
            ("Turbo.Tests.Rooms.Wired", "Turbo.Tests.Catalog."),
            ("Turbo.Tests.Rooms.WiredClock",),
            (),
        )
        with patch.object(ci_test_shards, "SHARDS", shards):
            self.assertEqual(2, ci_test_shards.shard_of(NAMES[0]))
            self.assert_each_name_in_one_shard()

    def test_a_shard_outside_the_list_is_refused(self):
        with self.assertRaises(ValueError):
            ci_test_shards.test_filter(len(ci_test_shards.SHARDS))


if __name__ == "__main__":
    unittest.main()
