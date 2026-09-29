"""Judge audit: consistency (same patch judged twice) and discrimination (controls)."""
import json, sys
import judge, run_eval

WRONG = '''diff --git a/Turbo.PacketHandlers/Room/Engine/MoveObjectMessageHandler.cs b/Turbo.PacketHandlers/Room/Engine/MoveObjectMessageHandler.cs
--- a/Turbo.PacketHandlers/Room/Engine/MoveObjectMessageHandler.cs
+++ b/Turbo.PacketHandlers/Room/Engine/MoveObjectMessageHandler.cs
@@ -20,6 +20,12 @@
+        // work around the issue here
+        try {
+            await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
+            var row = await db.Furniture.FindAsync(message.ObjectId);
+            row!.Z += 1.0; await db.SaveChangesAsync(ct);
+        } catch { }
'''
IRRELEVANT = "diff --git a/README.md b/README.md\n--- a/README.md\n+++ b/README.md\n@@ -1,1 +1,2 @@\n+Fixed.\n"
model = sys.argv[1] if len(sys.argv) > 1 else "claude-sonnet-5-5"
out = []
for case in run_eval.load_cases(None):
    ref = run_eval.reference_patch(case)
    obj = {"hidden_tests": "all passed", "quality_gate": "passed", "static_findings": []}
    runs = [judge.run_judge(model, case["task"], ref, ref, obj, case.get("judge_only_requirements", [])) for _ in range(2)]
    v = [r["verdict"] for r in runs]
    flips = [c1["id"] for c1, c2 in zip(v[0]["claims"], v[1]["claims"]) if c1["verdict"] != c2["verdict"]]
    bad = judge.run_judge(model, case["task"], ref, WRONG, {"hidden_tests": "failed", "quality_gate": "failed", "static_findings": ["critical handler-db-access", "critical silent-catch"]}, case.get("judge_only_requirements", []))
    irr = judge.run_judge(model, case["task"], ref, IRRELEVANT, {"hidden_tests": "failed", "quality_gate": "passed", "static_findings": []}, case.get("judge_only_requirements", []))
    row = {"case": case["id"], "oracle": [round(x["pass_rate"], 2) for x in v], "flipped_claims": flips,
           "wrong": round(bad["verdict"]["pass_rate"], 2), "irrelevant": round(irr["verdict"]["pass_rate"], 2),
           "cost": round(sum(r["judge_cost_usd"] or 0 for r in runs + [bad, irr]), 3),
           "oracle_no": [c["id"] for c in v[0]["claims"] if c["verdict"] == "no"]}
    print(json.dumps(row), flush=True)
    out.append(row)
json.dump(out, open(sys.argv[2] if len(sys.argv) > 2 else "/tmp/turbo-eval/judge_audit.json", "w"), indent=1)
