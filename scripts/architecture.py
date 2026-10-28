"""Inventory declared project references and enforce a small, explicit boundary policy."""

import argparse
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
POLICY = Path(__file__).with_name("architecture_policy.json")


def project_graph(root):
    graph = {}
    for project in sorted(root.glob("Turbo.*/*.csproj")):
        references = []
        for item in ET.parse(project).iter("ProjectReference"):
            include = item.attrib.get("Include", "")
            if not include or "$" in include or ";" in include:
                raise ValueError(f"Unresolved project reference in {project.name}")
            references.append(Path(include.replace("\\", "/")).stem)
        graph[project.stem] = sorted(set(references))
    if not graph:
        raise ValueError("No Turbo project files found")
    for source, targets in graph.items():
        for target in targets:
            if target not in graph:
                raise ValueError(f"Unmapped project reference: {source} -> {target}")
    return graph


def violations(graph, policy):
    found = set()
    for rule in policy["forbidden_dependencies"]:
        if rule["target"] in graph.get(rule["source"], []):
            found.add((rule["source"], rule["target"], rule["rule"]))
    allowed = {tuple(edge) for edge in policy["allowed_contract_dependencies"]}
    for source in policy["contract_projects"]:
        for target in graph.get(source, []):
            if (source, target) not in allowed:
                found.add((source, target, "contract-implementation"))
    return found


def check(graph, policy):
    found = violations(graph, policy)
    baseline = {(v["source"], v["target"], v["rule"]) for v in policy["baseline"]}
    return {
        "new_violations": sorted(found - baseline),
        "existing_violations": sorted(found & baseline),
        "stale_baseline": sorted(baseline - found),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("map", "check"))
    args = parser.parse_args()
    graph = project_graph(ROOT)
    result = graph if args.command == "map" else check(graph, json.loads(POLICY.read_text()))
    print(json.dumps(result, indent=2))
    if args.command == "check" and (result["new_violations"] or result["stale_baseline"]):
        return 1
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, ET.ParseError) as error:
        print(f"Architecture check failed: {error}", file=sys.stderr)
        sys.exit(2)
