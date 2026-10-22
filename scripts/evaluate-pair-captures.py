"""Offline comparison on an explicitly labeled tuning matrix. Never calls a provider.

Reranks only the baseline-selected set; rejects unlabeled or unscored pairs.
Criteria gold is optional and must be authored separately from model outputs.
"""
import argparse
import hashlib
import json
import math
import pathlib
import statistics
from datetime import datetime, timezone


def read(path):
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()


def dcg(values):
    return sum((2 ** value - 1) / math.log2(i + 2) for i, value in enumerate(values))


def ranking_metrics(order, labels, k):
    """labels includes the entire judged candidate set; missing != negative."""
    if len(order) != len(set(order)) or any(key not in labels for key in order):
        raise ValueError("duplicate_or_unlabeled_selected_pair")
    if not labels or any(not isinstance(v, int) or v < 0 or v > 3 for v in labels.values()):
        raise ValueError("invalid_labels")
    selected = order[:k]
    positive = sum(value > 0 for value in labels.values())
    ideal = dcg(sorted(labels.values(), reverse=True)[:k])
    return {
        "selectionRecall": sum(labels[key] > 0 for key in selected) / positive if positive else None,
        "top1Relevant": int(bool(selected) and labels[selected[0]] > 0),
        "ndcgAtK": dcg([labels[key] for key in selected]) / ideal if ideal else None,
        "returnedCount": len(selected),
    }


def average(rows):
    return {key: statistics.mean(values) if (values := [r[key] for r in rows if r[key] is not None]) else None
            for key in rows[0]}


def evaluate(dataset_path, baseline_path, captures_path, gold_path=None):
    dataset, baseline, captures = map(read, [dataset_path, baseline_path, captures_path])
    if dataset["split"] != "tuning" or baseline["split"] != "tuning":
        raise ValueError("tuning_only_no_final_holdout")
    if baseline["datasetId"] != dataset["datasetId"] or baseline["inputSha256"] != sha(dataset_path):
        raise ValueError("baseline_input_mismatch")
    resumes = {x["id"]: x["groupId"] for x in dataset["resumes"]}
    jobs = {x["id"]: x["groupId"] for x in dataset["jobs"]}
    if len(resumes) != len(dataset["resumes"]) or len(jobs) != len(dataset["jobs"]) or \
            len(set(resumes.values())) != len(resumes) or len(set(jobs.values())) != len(jobs):
        raise ValueError("use_one_representative_per_group")
    gold_labels = {(x["resumeGroupId"], x["jobGroupId"]): x["relevance"] for x in dataset["labels"]}
    if len(gold_labels) != len(dataset["labels"]):
        raise ValueError("duplicate_gold_pair")
    if set(gold_labels) != {(r, j) for r in resumes.values() for j in jobs.values()}:
        raise ValueError("complete_judgments_required_no_implicit_negatives")
    by_pair = {}
    for row in captures:
        capture = row["capture"]
        if capture["view"]["status"] != "succeeded" or capture["view"]["score"] is None:
            raise ValueError("failed_or_unscored_capture")
        if row["context"] == "self":
            key = (row["resumeId"], row["jobId"])
            if key in by_pair:
                raise ValueError("duplicate_capture")
            by_pair[key] = capture
    if set(by_pair) != {(r, j) for r in resumes for j in jobs}:
        raise ValueError("complete_self_matrix_required")
    methods = {x["method"]["configHash"] for x in by_pair.values()}
    if len(methods) != 1:
        raise ValueError("mixed_methods")
    comparisons = {}
    k = baseline["topK"]
    for direction, queries, candidates in [("cvToJobs", resumes, jobs), ("jobsToResumes", jobs, resumes)]:
        base_rows, deep_rows, query_rows = [], [], []
        for query, group in queries.items():
            selected = sorted((x for x in baseline[direction]["topResults"] if x["queryGroupId"] == group), key=lambda x: x["rank"])
            order = [x["jobId"] if direction == "cvToJobs" else x["resumeId"] for x in selected]
            pair = lambda target: (query, target) if direction == "cvToJobs" else (target, query)
            labels = {target: gold_labels[(resumes[pair(target)[0]], jobs[pair(target)[1]])] for target in candidates}
            ranked = sorted(order, key=lambda target: (-by_pair[pair(target)]["view"]["score"], order.index(target)))
            base_rows.append(ranking_metrics(order, labels, k))
            deep_rows.append(ranking_metrics(ranked, labels, k))
            query_rows.append({"queryId": query, "baselineOrder": order, "rerankedOrder": ranked})
        comparisons[direction] = {"baseline": average(base_rows), "rerankedSelectedSet": average(deep_rows), "queries": query_rows}
    usage = [x["capture"]["usage"] for x in captures]
    if len({x["capture"]["view"]["id"] for x in captures}) != len(captures):
        raise ValueError("duplicate_job_would_double_count_cost")
    units = {x["unit"] for x in usage}
    if len(units) != 1:
        raise ValueError("cannot_mix_usd_and_test_credit")
    latencies = sorted(x["elapsedMs"] for x in usage if x["elapsedMs"] is not None and not x["cacheHit"])
    report = {
        "schemaVersion": 1, "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "datasetId": dataset["datasetId"], "split": "tuning", "representativeQualityAccepted": False,
        "inputs": {"datasetSha256": sha(dataset_path), "baselineSha256": sha(baseline_path), "capturesSha256": sha(captures_path), "evaluatorSha256": sha(__file__)},
        "baselineMethod": baseline["methodVersion"], "pairMethod": next(iter(methods)), "topK": k,
        "scoreMatrix": [{"resumeId": r, "jobId": j, "score": c["view"]["score"]} for (r, j), c in by_pair.items()],
        "comparison": comparisons,
        "usage": {"jobs": len(captures), "calls": sum(x["calls"] for x in usage), "unknownCalls": sum(x["unknownCalls"] for x in usage),
                  "unit": next(iter(units)), "knownAmount": sum(x["amount"] or 0 for x in usage),
                  "inputTokens": sum(x["inputTokens"] or 0 for x in usage), "outputTokens": sum(x["outputTokens"] or 0 for x in usage),
                  "latencySamples": len(latencies), "medianElapsedMs": statistics.median(latencies) if latencies else None,
                  "p95ElapsedMsNearestRank": latencies[math.ceil(.95 * len(latencies)) - 1] if latencies else None},
        "limits": ["Tiny synthetic IT tuning matrix; not held-out quality evidence.",
                   "Reranking reuses self-context scores with identical eligible facts; recruiter pilot covers diagonal pairs only.",
                   "End-to-end latency includes queue time; no provider-only latency claim."]}
    if gold_path:
        gold = read(gold_path)
        checked = positive = unsupported = mismatch = bad_quote = 0
        for r, j in by_pair:
            expected = gold["pairs"][r + ":" + j]
            capture = by_pair[r, j]
            facts = {x["key"]: x["value"] for x in capture["payload"]["facts"]}
            for criterion in capture["view"]["criteria"]:
                status = expected[criterion["requirement"]]
                checked += 1
                positive += criterion["status"] in ("met", "partial")
                unsupported += criterion["status"] in ("met", "partial") and status == "no_evidence"
                mismatch += criterion["status"] != status
                bad_quote += sum(facts.get(e["key"]) != e["value"] for e in criterion["evidence"])
        report["criterionDiagnostics"] = {"goldSha256": sha(gold_path), "goldProvenance": gold["provenance"], "checked": checked,
            "positiveClaims": positive, "unsupportedPositiveClaims": unsupported, "statusMismatches": mismatch, "invalidQuotes": bad_quote}
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("dataset", "baseline", "captures", "output"):
        parser.add_argument("--" + name, required=True)
    parser.add_argument("--criterion-gold")
    args = parser.parse_args()
    result = evaluate(args.dataset, args.baseline, args.captures, args.criterion_gold)
    pathlib.Path(args.output).write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": args.output, "comparison": result["comparison"], "usage": result["usage"]}, ensure_ascii=False))
