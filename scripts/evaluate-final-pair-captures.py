"""Fail-closed, offline final pair comparison. Never calls a provider or a database.

Only an approved final freeze envelope can unlock the explicitly hash-pinned inputs.
This validates declarations and captures; it cannot grant rights or accept quality.
"""
import argparse
from decimal import Decimal, InvalidOperation
import hashlib
import json
import math
from pathlib import Path
import re
import statistics
from datetime import datetime, timezone


ROOT = Path(__file__).resolve().parents[1]
SHA = re.compile(r"[0-9a-f]{64}\Z")


def parse_json(raw):
    def reject_constant(_):
        raise ValueError("nonfinite_json_number")
    def unique_members(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate_json_key")
            result[key] = value
        return result
    return json.loads(raw.decode("utf-8-sig"), parse_float=Decimal,
                      parse_constant=reject_constant, object_pairs_hook=unique_members)


def read_json(path):
    return parse_json(Path(path).read_bytes())


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def identifier(value):
    if not isinstance(value, str) or not value.strip() or len(value) > 256:
        raise ValueError("invalid_identifier")
    return value


def digest(value):
    if not isinstance(value, str) or not SHA.fullmatch(value):
        raise ValueError("invalid_sha256")
    return value


def integer(value, minimum=0, maximum=None):
    if type(value) is not int or value < minimum or maximum is not None and value > maximum:
        raise ValueError("invalid_integer")
    return value


def number(value, maximum=None, allow_string=False):
    if type(value) is bool or not isinstance(value, (int, Decimal) + ((str,) if allow_string else ())):
        raise ValueError("invalid_number")
    try:
        parsed = Decimal(value)
    except (InvalidOperation, ValueError):
        raise ValueError("invalid_number") from None
    if not parsed.is_finite() or parsed < 0 or maximum is not None and parsed > maximum:
        raise ValueError("number_out_of_range")
    return parsed


def bounded(path, private_root):
    resolved = Path(path).resolve()
    if private_root is not None and not resolved.is_relative_to(Path(private_root).resolve()):
        raise ValueError("private_input_or_output_required")
    return resolved


def load_freeze(path, private_root=None, output_path=None):
    """No dataset, baseline, or capture bytes are read before this gate passes."""
    path = bounded(path, private_root)
    freeze_bytes = path.read_bytes()
    freeze = parse_json(freeze_bytes)
    if not isinstance(freeze, dict) or type(freeze.get("schema_version")) is not int or freeze["schema_version"] != 1:
        raise ValueError("freeze_schema_required")
    if freeze.get("status") != "final_frozen":
        raise ValueError("final_freeze_required")
    for name in ("owner_scope_approved", "final_independence_verified", "provider_rights_verified"):
        if freeze.get(name) is not True:
            raise ValueError("freeze_approval_and_rights_required")
    pair_method = digest(freeze.get("expected_method_config_hash"))
    baseline_method = identifier(freeze.get("expected_baseline_method_version"))
    baseline_config = digest(freeze.get("expected_baseline_configuration_hash"))
    k = integer(freeze.get("top_k"), 1)
    positive_min = integer(freeze.get("positive_relevance_min"), 1, 3)
    inputs = freeze.get("inputs")
    if not isinstance(inputs, dict):
        raise ValueError("freeze_inputs_required")
    paths, hashes = {}, {}
    # Validate every metadata entry before opening even the first input file.
    for name in ("dataset", "baseline", "captures"):
        entry = inputs.get(name)
        if not isinstance(entry, dict):
            raise ValueError("freeze_input_metadata_required")
        relative = identifier(entry.get("path"))
        hashes[name] = digest(entry.get("sha256"))
        paths[name] = bounded(path.parent / relative, private_root)
    if len(set(paths.values())) != 3 or path in paths.values():
        raise ValueError("distinct_inputs_required")
    if output_path is not None:
        output = bounded(output_path, private_root)
        if output == path or output in paths.values():
            raise ValueError("output_must_not_overwrite_inputs")
    return freeze, paths, hashes, pair_method, baseline_method, baseline_config, k, positive_min, hashlib.sha256(freeze_bytes).hexdigest()


def representatives(rows):
    if not isinstance(rows, list) or not rows:
        raise ValueError("representatives_required")
    result = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("invalid_representative")
        identity, group = identifier(row.get("id")), identifier(row.get("groupId"))
        if identity in result or group in result.values():
            raise ValueError("one_representative_per_group_required")
        result[identity] = group
    return result


def pair_bindings(dataset, resumes, jobs):
    """Bind wrapper IDs to frozen source versions/revisions and exact payloads."""
    versions = {}
    for kind, ids, field in (("resumes", resumes, "resumeVersionId"), ("jobs", jobs, "jobVersionId")):
        seen_versions = set()
        for row in dataset[kind]:
            version = identifier(row.get(field))
            digest(row.get("sourceSha256"))
            if version in seen_versions:
                raise ValueError("duplicate_source_version")
            seen_versions.add(version)
            versions[kind, row["id"]] = version
    rows = dataset.get("pairBindings")
    if not isinstance(rows, list):
        raise ValueError("frozen_pair_bindings_required")
    result = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("invalid_pair_binding")
        key = identifier(row.get("resumeId")), identifier(row.get("jobId"))
        if key[0] not in resumes or key[1] not in jobs or key in result:
            raise ValueError("duplicate_or_unknown_pair_binding")
        expected = {field: identifier(row.get(field)) for field in
                    ("resumeVersionId", "jobVersionId", "resumeRevisionId", "jobRevisionId")}
        if (expected["resumeVersionId"] != versions["resumes", key[0]] or
                expected["jobVersionId"] != versions["jobs", key[1]]):
            raise ValueError("source_version_binding_mismatch")
        expected["inputFingerprint"] = digest(row.get("inputFingerprint"))
        result[key] = expected
    if set(result) != {(r, j) for r in resumes for j in jobs}:
        raise ValueError("complete_frozen_pair_bindings_required")
    # The same canonical document must use one confirmed revision in all pairs.
    for ids, field in ((resumes, "resumeRevisionId"), (jobs, "jobRevisionId")):
        index = 0 if field == "resumeRevisionId" else 1
        for identity in ids:
            if len({row[field] for key, row in result.items() if key[index] == identity}) != 1:
                raise ValueError("inconsistent_source_revision_binding")
    return result


def dcg(values):
    return sum((2 ** value - 1) / math.log2(index + 2) for index, value in enumerate(values))


def ranking_metrics(order, labels, k, positive_min):
    if len(order) != len(set(order)) or any(target not in labels for target in order) or len(order) > k:
        raise ValueError("duplicate_unjudged_or_excess_selected_pair")
    positive_count = sum(value >= positive_min for value in labels.values())
    retrieved = sum(labels[target] >= positive_min for target in order)
    ideal = dcg(sorted(labels.values(), reverse=True)[:k])
    precision_denominator = min(k, len(labels))
    return {
        "candidateCount": len(labels), "judgedCandidateCount": len(labels),
        "judgmentCoverage": 1.0, "labeledPositiveCount": positive_count,
        "selectedCount": len(order), "positiveSelectedCount": retrieved,
        "selectedLabelCoverage": 1.0 if order else None,
        "precisionDenominator": precision_denominator,
        "selectionRecall": retrieved / positive_count if positive_count else None,
        "precisionAtK": retrieved / precision_denominator,
        "top1Relevant": int(bool(order) and labels[order[0]] >= positive_min),
        "idealDcgAtK": ideal, "ndcgAtK": dcg([labels[target] for target in order]) / ideal if ideal else None,
    }


def mean_available(rows, key):
    values = [row[key] for row in rows if row[key] is not None]
    return statistics.mean(values) if values else None


def summarize(rows):
    return {
        "queryCount": len(rows),
        "queriesWithLabeledPositives": sum(row["labeledPositiveCount"] > 0 for row in rows),
        "zeroPositiveQueries": sum(row["labeledPositiveCount"] == 0 for row in rows),
        "queriesWithNonzeroIdealDcg": sum(row["idealDcgAtK"] > 0 for row in rows),
        "queriesWithResults": sum(row["selectedCount"] > 0 for row in rows),
        "judgedCandidatePairs": sum(row["judgedCandidateCount"] for row in rows),
        "selectedPairs": sum(row["selectedCount"] for row in rows),
        "positiveSelectedPairs": sum(row["positiveSelectedCount"] for row in rows),
        "judgmentCoverage": 1.0,
        "selectedLabelCoverage": 1.0 if any(row["selectedCount"] for row in rows) else None,
        "meanSelectionRecallOnPositiveQueries": mean_available(rows, "selectionRecall"),
        "meanPrecisionAtKOnAllQueries": mean_available(rows, "precisionAtK"),
        "top1RelevantRateOnAllQueries": mean_available(rows, "top1Relevant"),
        "meanNdcgAtKOnNonzeroIdealQueries": mean_available(rows, "ndcgAtK"),
        "recallDenominatorQueries": sum(row["selectionRecall"] is not None for row in rows),
        "precisionDenominatorQueries": len(rows),
        "top1DenominatorQueries": len(rows),
        "ndcgDenominatorQueries": sum(row["ndcgAtK"] is not None for row in rows),
    }


def baseline_orders(baseline, direction, resumes, jobs, k):
    queries, candidates = (resumes, jobs) if direction == "cvToJobs" else (jobs, resumes)
    block = baseline.get(direction)
    if not isinstance(block, dict) or not isinstance(block.get("topResults"), list):
        raise ValueError("baseline_direction_required")
    groups = {group: identity for identity, group in queries.items()}
    rows = {query: [] for query in queries}
    for row in block["topResults"]:
        if not isinstance(row, dict):
            raise ValueError("invalid_baseline_row")
        r, j = row.get("resumeId"), row.get("jobId")
        query, target = (r, j) if direction == "cvToJobs" else (j, r)
        if query not in queries or target not in candidates or row.get("queryGroupId") not in groups or groups[row["queryGroupId"]] != query:
            raise ValueError("baseline_pair_or_query_mismatch")
        if row.get("resumeGroupId") != resumes[r] or row.get("jobGroupId") != jobs[j]:
            raise ValueError("baseline_group_mismatch")
        rank = integer(row.get("rank"), 1, k)
        number(row.get("retrievalScore"), Decimal(1))
        rows[query].append((rank, target))
    orders = {}
    for query, values in rows.items():
        values.sort()
        ranks = [rank for rank, _ in values]
        order = [target for _, target in values]
        if ranks != list(range(1, len(values) + 1)) or len(order) != len(set(order)):
            raise ValueError("duplicate_or_noncontiguous_baseline_ranks")
        orders[query] = order
    return orders


def usage_summary(captures):
    usages = []
    for capture in captures:
        usage = capture.get("usage")
        if not isinstance(usage, dict):
            raise ValueError("usage_required")
        if usage.get("unit") not in ("usd", "test_credit"):
            raise ValueError("unsupported_usage_unit")
        calls, unknown = integer(usage.get("calls")), integer(usage.get("unknownCalls"))
        if unknown > calls or type(usage.get("cacheHit")) is not bool:
            raise ValueError("invalid_call_or_cache_metadata")
        if usage["cacheHit"] and calls != 0:
            raise ValueError("cache_hit_cannot_have_calls")
        amount = None if usage.get("amount") is None else number(usage["amount"], allow_string=True)
        tokens = {name: None if usage.get(name) is None else integer(usage[name]) for name in ("inputTokens", "outputTokens")}
        elapsed = None if usage.get("elapsedMs") is None else number(usage["elapsedMs"])
        usages.append({"unit": usage["unit"], "calls": calls, "unknownCalls": unknown,
                       "amount": amount, "cacheHit": usage["cacheHit"], "elapsedMs": elapsed, **tokens})
    units = {row["unit"] for row in usages}
    if len(units) != 1:
        raise ValueError("cannot_mix_usd_and_test_credit")
    amounts = [row["amount"] for row in usages if row["amount"] is not None]
    known = sum(amounts, Decimal(0)) if amounts else None
    unknown_amounts = sum(row["amount"] is None for row in usages)
    unknown_calls = sum(row["unknownCalls"] for row in usages)
    has_unknown = bool(unknown_amounts or unknown_calls)
    latencies = sorted(float(row["elapsedMs"]) for row in usages if row["elapsedMs"] is not None and not row["cacheHit"])
    result = {
        "jobs": len(usages), "calls": sum(row["calls"] for row in usages), "unknownCalls": unknown_calls,
        "unit": next(iter(units)), "amountEncoding": "decimal_string_or_null",
        "knownAmount": format(known, "f") if known is not None else None,
        "knownAmountJobs": len(amounts), "unknownAmountJobs": unknown_amounts,
        "hasUnknownCost": has_unknown,
        "totalAmount": format(known, "f") if not has_unknown and known is not None else None,
        "latencySamplesUncached": len(latencies),
        "cacheHitJobsExcludedFromLatency": sum(row["cacheHit"] for row in usages),
        "unknownUncachedLatencyJobs": sum(row["elapsedMs"] is None and not row["cacheHit"] for row in usages),
        "medianElapsedMs": statistics.median(latencies) if latencies else None,
        "p95ElapsedMsNearestRank": latencies[math.ceil(.95 * len(latencies)) - 1] if latencies else None,
    }
    for token in ("inputTokens", "outputTokens"):
        values = [row[token] for row in usages if row[token] is not None]
        result[token] = sum(values) if values else None
        result[token + "UnknownJobs"] = len(usages) - len(values)
    return result


def evaluate(freeze_path, *, private_root=None, output_path=None):
    freeze, paths, hashes, method, baseline_method, baseline_config, k, positive_min, freeze_hash = load_freeze(freeze_path, private_root, output_path)
    # Hash and parse the same byte snapshots: files may change after the read.
    snapshots = {name: paths[name].read_bytes() for name in ("dataset", "baseline", "captures")}
    if any(hashlib.sha256(snapshots[name]).hexdigest() != hashes[name] for name in snapshots):
        raise ValueError("frozen_input_hash_mismatch")
    dataset, baseline, capture_envelope = (parse_json(snapshots[name]) for name in ("dataset", "baseline", "captures"))
    if not all(isinstance(item, dict) for item in (dataset, baseline, capture_envelope)):
        raise ValueError("input_objects_required")
    if dataset.get("split") != "final" or baseline.get("split") != "final" or capture_envelope.get("split") != "final":
        raise ValueError("final_split_required")
    dataset_id = identifier(dataset.get("datasetId"))
    if baseline.get("datasetId") != dataset_id or baseline.get("inputSha256") != hashes["dataset"] or capture_envelope.get("datasetSha256") != hashes["dataset"]:
        raise ValueError("same_final_dataset_hash_required")
    if type(capture_envelope.get("schemaVersion")) is not int or capture_envelope["schemaVersion"] != 1:
        raise ValueError("capture_envelope_schema_required")
    if baseline.get("methodVersion") != baseline_method or baseline.get("configurationHash") != baseline_config:
        raise ValueError("frozen_baseline_method_mismatch")
    if type(baseline.get("topK")) is not int or baseline["topK"] != k:
        raise ValueError("frozen_top_k_mismatch")
    resumes, jobs = representatives(dataset.get("resumes")), representatives(dataset.get("jobs"))
    bindings = pair_bindings(dataset, resumes, jobs)
    labels, rows = {}, dataset.get("labels")
    if not isinstance(rows, list):
        raise ValueError("complete_judgments_required")
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("invalid_label")
        key = (identifier(row.get("resumeGroupId")), identifier(row.get("jobGroupId")))
        if key in labels:
            raise ValueError("duplicate_gold_pair")
        labels[key] = integer(row.get("relevance"), 0, 3)
    if set(labels) != {(r, j) for r in resumes.values() for j in jobs.values()}:
        raise ValueError("complete_judgments_required_no_implicit_negatives")
    rows = capture_envelope.get("captures")
    if not isinstance(rows, list):
        raise ValueError("capture_matrix_required")
    by_pair, job_ids = {}, set()
    for row in rows:
        if not isinstance(row, dict) or row.get("context") != "self":
            raise ValueError("self_context_matrix_only")
        pair = row.get("resumeId"), row.get("jobId")
        if pair[0] not in resumes or pair[1] not in jobs or pair in by_pair:
            raise ValueError("duplicate_or_unknown_capture_pair")
        capture = row.get("capture")
        if not isinstance(capture, dict) or not isinstance(capture.get("view"), dict) or not isinstance(capture.get("method"), dict):
            raise ValueError("invalid_capture")
        view = capture["view"]
        if view.get("status") != "succeeded" or view.get("score") is None or view.get("errorCode") is not None:
            raise ValueError("failed_or_unscored_capture")
        if view.get("context", "self") != "self":
            raise ValueError("self_context_matrix_only")
        expected = bindings[pair]
        if any(view.get(field) != expected[field] for field in
               ("resumeVersionId", "jobVersionId", "resumeRevisionId", "jobRevisionId")) or capture.get("fingerprint") != expected["inputFingerprint"]:
            raise ValueError("capture_source_or_payload_binding_mismatch")
        score = number(view["score"], Decimal(100))
        if capture["method"].get("configHash") != method:
            raise ValueError("frozen_pair_method_mismatch")
        identity = identifier(view.get("id"))
        if identity in job_ids:
            raise ValueError("duplicate_job_would_double_count_cost")
        job_ids.add(identity)
        by_pair[pair] = {"capture": capture, "score": score}
    if set(by_pair) != {(r, j) for r in resumes for j in jobs}:
        raise ValueError("complete_self_matrix_required")
    comparison = {}
    for direction in ("cvToJobs", "jobsToResumes"):
        queries, candidates = (resumes, jobs) if direction == "cvToJobs" else (jobs, resumes)
        orders = baseline_orders(baseline, direction, resumes, jobs, k)
        base_metrics, reranked_metrics, query_results = [], [], []
        for query in queries:
            pair = lambda target: (query, target) if direction == "cvToJobs" else (target, query)
            target_labels = {target: labels[resumes[pair(target)[0]], jobs[pair(target)[1]]] for target in candidates}
            order = orders[query]
            # Stable sort preserves baseline rank on equal deep scores; no candidate expansion.
            reranked = sorted(order, key=lambda target: -by_pair[pair(target)]["score"])
            before = ranking_metrics(order, target_labels, k, positive_min)
            after = ranking_metrics(reranked, target_labels, k, positive_min)
            base_metrics.append(before)
            reranked_metrics.append(after)
            query_results.append({"queryId": query, "baselineOrder": order, "rerankedOrder": reranked,
                                  "baselineMetrics": before, "rerankedMetrics": after})
        comparison[direction] = {"baseline": summarize(base_metrics), "rerankedSelectedSet": summarize(reranked_metrics),
                                 "queries": query_results}
    return {
        "schemaVersion": 1, "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "split": "final", "datasetId": dataset_id, "freezeDeclarationsValidated": True,
        "providerCalled": False, "qualityAccepted": False, "representativeQualityAccepted": False,
        "inputs": {name + "Sha256": hashes[name] for name in paths} | {"freezeSha256": freeze_hash, "evaluatorSha256": sha(__file__)},
        "baselineMethod": baseline_method, "baselineConfigurationHash": baseline_config,
        "pairMethod": method, "topK": k, "positiveRelevanceMin": positive_min,
        "denominator": {"resumeGroups": len(resumes), "jobGroups": len(jobs), "judgedPairs": len(labels),
                        "successfulSelfCaptures": len(by_pair), "completeJudgmentMatrix": True, "completeScoreMatrix": True},
        "metricConventions": {"positive": "relevance >= frozen positiveRelevanceMin",
            "precisionAtK": "positive selected / min(k, candidate pool size); unreturned slots count zero",
            "recall": "mean over queries with labeled positives; null when none",
            "ndcg": "2^relevance-1 gain; ideal from entire judged pool; null ideal=0; mean over nonzero ideals",
            "top1": "mean over all queries, including empty and zero-positive queries",
            "ties": "deep-score ties preserve baseline rank", "selection": "rerank only the original baseline-selected set"},
        "comparison": comparison,
        "scoreEncoding": "decimal_string",
        "scoreMatrix": [{"resumeId": r, "jobId": j, "score": format(row["score"], "f")} for (r, j), row in by_pair.items()],
        "usage": usage_summary([row["capture"] for row in by_pair.values()]),
        "limits": ["No evidence/criterion quality is inferred without separately frozen evidence gold.",
                   "Complete captures describe successful matrix availability; pipeline failures/coverage must be reported separately.",
                   "Freeze flags are checked declarations, not independent proof of consent, rights, human labels or representative quality.",
                   "End-to-end uncached latency can include queue time; no provider-only latency claim."]}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--freeze", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(argv)
    try:
        output = bounded(args.output, ROOT / ".local")
        if output.exists():
            raise ValueError("output_exists")
        result = evaluate(args.freeze, private_root=ROOT / ".local", output_path=output)
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("x", encoding="utf-8") as stream:
            stream.write(json.dumps(result, ensure_ascii=False, allow_nan=False, indent=2) + "\n")
    except (ValueError, OSError, KeyError, TypeError) as error:
        code = str(error) if isinstance(error, ValueError) and re.fullmatch(r"[a-z0-9_]+", str(error)) else "input_unavailable_or_invalid"
        print(json.dumps({"status": "rejected", "error": code, "providerCalled": False}))
        return 2
    print(json.dumps({"status": "evaluated", "judgedPairs": result["denominator"]["judgedPairs"],
                      "successfulSelfCaptures": result["denominator"]["successfulSelfCaptures"],
                      "qualityAccepted": False, "providerCalled": False}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
