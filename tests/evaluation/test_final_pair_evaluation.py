import copy
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("final_pair_evaluator", ROOT / "scripts/evaluate-final-pair-captures.py")
EVALUATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EVALUATOR)
PAIR_METHOD, BASE_CONFIG = "a" * 64, "b" * 64


class FinalPairEvaluationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="careergap-final-evaluator-synthetic-")
        self.root = Path(self.temp.name)
        self.freeze_path = self.root / "freeze.json"
        self.dataset = {"datasetId": "synthetic-final-tool-tests", "split": "final",
                        "resumes": [{"id": "r1", "groupId": "rg1"}, {"id": "r2", "groupId": "rg2"}],
                        "jobs": [{"id": "j1", "groupId": "jg1"}, {"id": "j2", "groupId": "jg2"}],
                        "labels": [{"resumeGroupId": r, "jobGroupId": j, "relevance": value}
                                   for r, j, value in (("rg1", "jg1", 3), ("rg1", "jg2", 0), ("rg2", "jg1", 0), ("rg2", "jg2", 0))]}
        for kind, field in (("resumes", "resumeVersionId"), ("jobs", "jobVersionId")):
            for row in self.dataset[kind]:
                row[field] = row["id"] + "-version"
                row["sourceSha256"] = hashlib.sha256(row["id"].encode()).hexdigest()
        self.dataset["pairBindings"] = [
            {"resumeId": f"r{r}", "jobId": f"j{j}", "resumeVersionId": f"r{r}-version",
             "jobVersionId": f"j{j}-version", "resumeRevisionId": f"r{r}-revision",
             "jobRevisionId": f"j{j}-revision", "inputFingerprint": hashlib.sha256(f"{r}-{j}".encode()).hexdigest()}
            for r in (1, 2) for j in (1, 2)]
        self.baseline = {"datasetId": self.dataset["datasetId"], "split": "final", "topK": 2,
                         "methodVersion": "synthetic-baseline-v1", "configurationHash": BASE_CONFIG,
                         "cvToJobs": {"topResults": []}, "jobsToResumes": {"topResults": []}}
        for direction in ("cvToJobs", "jobsToResumes"):
            for query in (1, 2):
                for rank, target in enumerate((2, 1), 1):
                    r, j = (query, target) if direction == "cvToJobs" else (target, query)
                    self.baseline[direction]["topResults"].append({
                        "queryGroupId": f"rg{query}" if direction == "cvToJobs" else f"jg{query}",
                        "resumeId": f"r{r}", "resumeGroupId": f"rg{r}",
                        "jobId": f"j{j}", "jobGroupId": f"jg{j}", "rank": rank, "retrievalScore": 1 / rank})
        self.captures = {"schemaVersion": 1, "split": "final", "captures": []}
        for r in (1, 2):
            for j in (1, 2):
                self.captures["captures"].append({"context": "self", "resumeId": f"r{r}", "jobId": f"j{j}",
                    "capture": {"view": {"id": f"job-{r}-{j}", "context": "self", "status": "succeeded", "score": 90 if (r, j) == (1, 1) else 10},
                                "method": {"configHash": PAIR_METHOD},
                                "usage": {"calls": 1, "unknownCalls": 0, "unit": "test_credit", "amount": "0.1",
                                          "inputTokens": 10, "outputTokens": 2, "elapsedMs": (r * 2 + j) * 10, "cacheHit": False}}})
        for row, binding in zip(self.captures["captures"], self.dataset["pairBindings"]):
            row["capture"]["view"].update({name: binding[name] for name in
                ("resumeVersionId", "jobVersionId", "resumeRevisionId", "jobRevisionId")})
            row["capture"]["fingerprint"] = binding["inputFingerprint"]
        self.freeze = {"schema_version": 1, "status": "final_frozen", "owner_scope_approved": True,
                       "final_independence_verified": True, "provider_rights_verified": True,
                       "expected_method_config_hash": PAIR_METHOD,
                       "expected_baseline_method_version": "synthetic-baseline-v1",
                       "expected_baseline_configuration_hash": BASE_CONFIG,
                       "top_k": 2, "positive_relevance_min": 1, "inputs": {}}
        self.write_inputs()

    def tearDown(self):
        self.temp.cleanup()

    def save(self, name, value):
        path = self.root / name
        path.write_text(json.dumps(value) + "\n", encoding="utf-8")
        return path

    def write_inputs(self):
        dataset_path = self.save("dataset.json", self.dataset)
        dataset_hash = hashlib.sha256(dataset_path.read_bytes()).hexdigest()
        self.baseline["inputSha256"] = dataset_hash
        self.captures["datasetSha256"] = dataset_hash
        self.save("baseline.json", self.baseline)
        self.save("captures.json", self.captures)
        for name in ("dataset", "baseline", "captures"):
            path = self.root / (name + ".json")
            self.freeze["inputs"][name] = {"path": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
        self.save("freeze.json", self.freeze)

    def evaluate(self):
        return EVALUATOR.evaluate(self.freeze_path)

    def block_input_reads(self, path):
        if path.resolve() != self.freeze_path.resolve():
            raise AssertionError("input_read_before_gate")
        return self.freeze_path_bytes

    def test_gate_rejects_before_reading_inputs(self):
        for field, value in (("status", "candidate"), ("owner_scope_approved", False),
                             ("final_independence_verified", False), ("provider_rights_verified", False),
                             ("owner_scope_approved", 1), ("schema_version", True)):
            with self.subTest(field=field, value=value):
                freeze = copy.deepcopy(self.freeze)
                freeze[field] = value
                self.save("freeze.json", freeze)
                self.freeze_path_bytes = self.freeze_path.read_bytes()
                with patch.object(Path, "read_bytes", autospec=True, side_effect=self.block_input_reads):
                    with self.assertRaises(ValueError):
                        self.evaluate()

    def test_all_input_metadata_is_checked_before_input_hash_read(self):
        self.freeze["inputs"]["captures"]["sha256"] = "invalid"
        self.save("freeze.json", self.freeze)
        self.freeze_path_bytes = self.freeze_path.read_bytes()
        with patch.object(Path, "read_bytes", autospec=True, side_effect=self.block_input_reads):
            with self.assertRaisesRegex(ValueError, "invalid_sha256"):
                self.evaluate()

    def test_duplicate_json_members_cannot_override_freeze_declarations(self):
        raw = json.dumps(self.freeze).replace('"status": "final_frozen"',
                                              '"status": "candidate", "status": "final_frozen"')
        self.freeze_path.write_text(raw, encoding="utf-8")
        self.freeze_path_bytes = self.freeze_path.read_bytes()
        with patch.object(Path, "read_bytes", autospec=True, side_effect=self.block_input_reads):
            with self.assertRaisesRegex(ValueError, "duplicate_json_key"):
                self.evaluate()

    def test_invalid_frozen_numbers_and_method_metadata_fail_before_inputs(self):
        for field, value in (("top_k", 0), ("top_k", True), ("positive_relevance_min", 0),
                             ("positive_relevance_min", 4), ("positive_relevance_min", True),
                             ("expected_method_config_hash", "bad"),
                             ("expected_baseline_configuration_hash", "bad"),
                             ("expected_baseline_method_version", "")):
            with self.subTest(field=field, value=value):
                freeze = copy.deepcopy(self.freeze)
                freeze[field] = value
                self.save("freeze.json", freeze)
                self.freeze_path_bytes = self.freeze_path.read_bytes()
                with patch.object(Path, "read_bytes", autospec=True, side_effect=self.block_input_reads):
                    with self.assertRaises(ValueError): self.evaluate()

    def test_hash_mismatch_fails_before_source_json_parse(self):
        (self.root / "dataset.json").write_text("corrupted", encoding="utf-8")
        with patch.object(EVALUATOR, "parse_json", wraps=EVALUATOR.parse_json) as read:
            with self.assertRaisesRegex(ValueError, "frozen_input_hash_mismatch"):
                self.evaluate()
        self.assertEqual([self.freeze_path.read_bytes()], [call.args[0] for call in read.call_args_list])

    def test_captures_must_match_frozen_versions_revisions_and_payload(self):
        for field in ("resumeVersionId", "jobVersionId", "resumeRevisionId", "jobRevisionId", "fingerprint"):
            for wrong in (None, "unrelated"):
                with self.subTest(field=field, wrong=wrong):
                    original = copy.deepcopy(self.captures)
                    capture = self.captures["captures"][0]["capture"]
                    target = capture if field == "fingerprint" else capture["view"]
                    target[field] = wrong
                    self.write_inputs()
                    with self.assertRaisesRegex(ValueError, "capture_source_or_payload_binding_mismatch"):
                        self.evaluate()
                    self.captures = original

    def test_dataset_requires_complete_consistent_source_and_payload_bindings(self):
        for fault in ("absent", "missing", "duplicate", "unknown", "wrong_version", "missing_source_hash", "revision", "fingerprint"):
            with self.subTest(fault=fault):
                original = copy.deepcopy(self.dataset)
                if fault == "absent": self.dataset.pop("pairBindings")
                if fault == "missing": self.dataset["pairBindings"].pop()
                if fault == "duplicate": self.dataset["pairBindings"].append(copy.deepcopy(self.dataset["pairBindings"][0]))
                if fault == "unknown": self.dataset["pairBindings"][0]["resumeId"] = "unknown"
                if fault == "wrong_version": self.dataset["pairBindings"][0]["resumeVersionId"] = "unrelated"
                if fault == "missing_source_hash": self.dataset["resumes"][0].pop("sourceSha256")
                if fault == "revision": self.dataset["pairBindings"][0]["resumeRevisionId"] = "other-revision"
                if fault == "fingerprint": self.dataset["pairBindings"][0]["inputFingerprint"] = "bad"
                self.write_inputs()
                with self.assertRaises(ValueError): self.evaluate()
                self.dataset = original

    def test_inputs_are_read_once_and_changed_files_cannot_change_hashed_payload(self):
        original_read = Path.read_bytes
        original_parse = EVALUATOR.parse_json
        counts = {}
        old_freeze_hash = hashlib.sha256(original_read(self.freeze_path)).hexdigest()

        def read_once(path):
            counts[path] = counts.get(path, 0) + 1
            if path.name in ("freeze.json", "dataset.json", "baseline.json", "captures.json") and counts[path] > 1:
                raise AssertionError("input_reopened_after_hash")
            return original_read(path)

        def parse_then_replace(raw):
            parsed = original_parse(raw)
            if isinstance(parsed, dict) and "cvToJobs" in parsed:
                changed = copy.deepcopy(self.baseline)
                changed["cvToJobs"]["topResults"] = []
                self.save("baseline.json", changed)
                changed_freeze = copy.deepcopy(self.freeze)
                changed_freeze["status"] = "candidate"
                self.save("freeze.json", changed_freeze)
            return parsed

        with patch.object(Path, "read_bytes", read_once), patch.object(EVALUATOR, "parse_json", parse_then_replace):
            report = self.evaluate()
        self.assertEqual(old_freeze_hash, report["inputs"]["freezeSha256"])
        self.assertEqual(["j2", "j1"], report["comparison"]["cvToJobs"]["queries"][0]["baselineOrder"])
        self.assertEqual(self.freeze["inputs"]["baseline"]["sha256"], report["inputs"]["baselineSha256"])
        self.assertNotEqual(report["inputs"]["baselineSha256"], hashlib.sha256(original_read(self.root / "baseline.json")).hexdigest())

    def test_denominators_and_baseline_same_convention_are_explicit(self):
        report = self.evaluate()
        for direction in ("cvToJobs", "jobsToResumes"):
            before, after = report["comparison"][direction]["baseline"], report["comparison"][direction]["rerankedSelectedSet"]
            self.assertEqual((2, 1, 1), (before["queryCount"], before["queriesWithLabeledPositives"], before["zeroPositiveQueries"]))
            self.assertEqual(1, before["recallDenominatorQueries"])
            self.assertEqual(1, before["ndcgDenominatorQueries"])
            self.assertEqual(2, before["top1DenominatorQueries"])
            self.assertEqual(1, before["meanSelectionRecallOnPositiveQueries"])
            self.assertEqual(.25, before["meanPrecisionAtKOnAllQueries"])
            self.assertEqual(0, before["top1RelevantRateOnAllQueries"])
            self.assertEqual(.5, after["top1RelevantRateOnAllQueries"])
            self.assertLess(before["meanNdcgAtKOnNonzeroIdealQueries"], after["meanNdcgAtKOnNonzeroIdealQueries"])
            zero = report["comparison"][direction]["queries"][1]["baselineMetrics"]
            self.assertIsNone(zero["selectionRecall"])
            self.assertIsNone(zero["ndcgAtK"])
        self.assertFalse(report["qualityAccepted"])
        self.assertFalse(report["providerCalled"])

    def test_stable_ties_preserve_baseline_and_cannot_recover_unselected_positive(self):
        full_orders = copy.deepcopy(self.baseline)
        for row in self.captures["captures"]:
            row["capture"]["view"]["score"] = 5
        self.freeze["top_k"] = self.baseline["topK"] = 1
        for direction in ("cvToJobs", "jobsToResumes"):
            self.baseline[direction]["topResults"] = [row for row in self.baseline[direction]["topResults"] if row["rank"] == 1]
        self.write_inputs()
        report = self.evaluate()
        for block in report["comparison"].values():
            self.assertEqual(0, block["rerankedSelectedSet"]["meanSelectionRecallOnPositiveQueries"])
            for query in block["queries"]:
                self.assertEqual(query["baselineOrder"], query["rerankedOrder"])
        self.freeze["top_k"] = self.baseline["topK"] = 2
        for direction in ("cvToJobs", "jobsToResumes"):
            self.baseline[direction]["topResults"] = full_orders[direction]["topResults"]
        self.write_inputs()
        for block in self.evaluate()["comparison"].values():
            for query in block["queries"]:
                self.assertEqual(2, len(query["baselineOrder"]))
                self.assertEqual(query["baselineOrder"], query["rerankedOrder"])

    def test_positive_threshold_and_unreturned_slots_use_frozen_convention(self):
        self.dataset["labels"][0]["relevance"] = 1
        self.freeze["positive_relevance_min"] = 2
        for direction in ("cvToJobs", "jobsToResumes"):
            self.baseline[direction]["topResults"] = [row for row in self.baseline[direction]["topResults"] if row["rank"] == 1]
        self.write_inputs()
        for block in self.evaluate()["comparison"].values():
            first = block["queries"][0]["baselineMetrics"]
            self.assertEqual(2, first["precisionDenominator"])
            self.assertEqual(1, first["selectedCount"])
            self.assertEqual(0, first["labeledPositiveCount"])
            self.assertIsNone(first["selectionRecall"])
            self.assertEqual(0, first["ndcgAtK"])

    def test_all_zero_and_empty_selections_have_null_recall_and_ndcg(self):
        for row in self.dataset["labels"]:
            row["relevance"] = 0
        for direction in ("cvToJobs", "jobsToResumes"):
            self.baseline[direction]["topResults"] = []
        self.write_inputs()
        for block in self.evaluate()["comparison"].values():
            for metric in (block["baseline"], block["rerankedSelectedSet"]):
                self.assertIsNone(metric["meanSelectionRecallOnPositiveQueries"])
                self.assertIsNone(metric["meanNdcgAtKOnNonzeroIdealQueries"])
                self.assertEqual(0, metric["meanPrecisionAtKOnAllQueries"])
                self.assertIsNone(metric["selectedLabelCoverage"])
                self.assertEqual(0, metric["recallDenominatorQueries"])

    def test_invalid_labels_and_matrix_are_rejected(self):
        for fault in ("missing", "duplicate", "bool", "out_of_range", "unknown_group", "duplicate_representative"):
            with self.subTest(fault=fault):
                original = copy.deepcopy(self.dataset)
                if fault == "missing": self.dataset["labels"].pop()
                if fault == "duplicate": self.dataset["labels"].append(copy.deepcopy(self.dataset["labels"][0]))
                if fault == "bool": self.dataset["labels"][0]["relevance"] = True
                if fault == "out_of_range": self.dataset["labels"][0]["relevance"] = 4
                if fault == "unknown_group": self.dataset["labels"][0]["jobGroupId"] = "unknown"
                if fault == "duplicate_representative": self.dataset["resumes"][1]["groupId"] = "rg1"
                self.write_inputs()
                with self.assertRaises(ValueError): self.evaluate()
                self.dataset = original

    def test_split_dataset_binding_and_method_binding_are_strict(self):
        for fault in ("dataset_split", "baseline_split", "capture_split", "baseline_binding", "capture_binding", "baseline_method", "baseline_config", "pair_method", "top_k"):
            with self.subTest(fault=fault):
                dataset, baseline, captures = copy.deepcopy(self.dataset), copy.deepcopy(self.baseline), copy.deepcopy(self.captures)
                if fault == "dataset_split": self.dataset["split"] = "tuning"
                if fault == "baseline_split": self.baseline["split"] = "tuning"
                if fault == "capture_split": self.captures["split"] = "tuning"
                if fault == "baseline_method": self.baseline["methodVersion"] = "other"
                if fault == "baseline_config": self.baseline["configurationHash"] = "c" * 64
                if fault == "pair_method": self.captures["captures"][0]["capture"]["method"]["configHash"] = "c" * 64
                if fault == "top_k": self.baseline["topK"] = 1
                self.write_inputs()
                if fault in ("baseline_binding", "capture_binding"):
                    name, value = ("baseline", self.baseline) if fault == "baseline_binding" else ("captures", self.captures)
                    value["inputSha256" if name == "baseline" else "datasetSha256"] = "f" * 64
                    path = self.save(name + ".json", value)
                    self.freeze["inputs"][name]["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
                    self.save("freeze.json", self.freeze)
                with self.assertRaises(ValueError): self.evaluate()
                self.dataset, self.baseline, self.captures = dataset, baseline, captures

    def test_invalid_capture_scores_status_context_and_duplicates(self):
        for fault in ("missing", "duplicate_pair", "duplicate_job", "failed", "null", "bool", "nan", "negative", "too_large", "string", "context"):
            with self.subTest(fault=fault):
                original = copy.deepcopy(self.captures)
                row = self.captures["captures"][0]
                view = row["capture"]["view"]
                if fault == "missing": self.captures["captures"].pop()
                if fault == "duplicate_pair": self.captures["captures"].append(copy.deepcopy(row))
                if fault == "duplicate_job": self.captures["captures"][1]["capture"]["view"]["id"] = view["id"]
                if fault == "failed": view.update(status="failed", score=0)
                if fault == "null": view["score"] = None
                if fault == "bool": view["score"] = False
                if fault == "nan": view["score"] = float("nan")
                if fault == "negative": view["score"] = -1
                if fault == "too_large": view["score"] = 101
                if fault == "string": view["score"] = "50"
                if fault == "context": row["context"] = "public_pool"
                self.write_inputs()
                with self.assertRaises(ValueError): self.evaluate()
                self.captures = original

    def test_invalid_baseline_pairs_ranks_and_duplicates(self):
        for fault in ("duplicate", "bad_rank", "gap_rank", "wrong_query", "wrong_group", "unknown_target", "invalid_score"):
            with self.subTest(fault=fault):
                original = copy.deepcopy(self.baseline)
                rows = self.baseline["cvToJobs"]["topResults"]
                if fault == "duplicate": rows.append(copy.deepcopy(rows[0]))
                if fault == "bad_rank": rows[0]["rank"] = True
                if fault == "gap_rank": rows[:] = [row for row in rows if row["rank"] == 2]
                if fault == "wrong_query": rows[0]["queryGroupId"] = "rg2"
                if fault == "wrong_group": rows[0]["jobGroupId"] = "jg1"
                if fault == "unknown_target": rows[0]["jobId"] = "unknown"
                if fault == "invalid_score": rows[0]["retrievalScore"] = 2
                self.write_inputs()
                with self.assertRaises(ValueError): self.evaluate()
                self.baseline = original

    def test_decimal_cost_exact_unknowns_not_zero_and_uncached_nearest_rank_p95(self):
        report = self.evaluate()
        self.assertEqual("0.4", report["usage"]["totalAmount"])
        self.assertEqual("0.4", report["usage"]["knownAmount"])
        self.assertEqual(60, report["usage"]["p95ElapsedMsNearestRank"])
        self.captures["captures"][0]["capture"]["usage"].update(cacheHit=True, calls=0, amount=0, elapsedMs=999)
        self.captures["captures"][1]["capture"]["usage"].update(amount=None, inputTokens=None, unknownCalls=1)
        self.write_inputs()
        usage = self.evaluate()["usage"]
        self.assertEqual("0.2", usage["knownAmount"])
        self.assertIsNone(usage["totalAmount"])
        self.assertTrue(usage["hasUnknownCost"])
        self.assertEqual(1, usage["unknownAmountJobs"])
        self.assertEqual(1, usage["unknownCalls"])
        self.assertEqual(1, usage["inputTokensUnknownJobs"])
        self.assertEqual(3, usage["latencySamplesUncached"])
        self.assertEqual(60, usage["p95ElapsedMsNearestRank"])
        for row in self.captures["captures"]:
            row["capture"]["usage"]["amount"] = None
            row["capture"]["usage"]["elapsedMs"] = None
        self.write_inputs()
        usage = self.evaluate()["usage"]
        self.assertIsNone(usage["knownAmount"])
        self.assertIsNone(usage["p95ElapsedMsNearestRank"])

    def test_mixed_units_or_invalid_usage_rejected(self):
        for fault in ("mixed", "unit", "negative", "bool", "unknown_gt_calls", "cache_calls", "token_bool", "latency_nan"):
            with self.subTest(fault=fault):
                original = copy.deepcopy(self.captures)
                usage = self.captures["captures"][0]["capture"]["usage"]
                if fault == "mixed": usage["unit"] = "usd"
                if fault == "unit": usage["unit"] = "credits"
                if fault == "negative": usage["amount"] = -1
                if fault == "bool": usage["amount"] = False
                if fault == "unknown_gt_calls": usage["unknownCalls"] = 2
                if fault == "cache_calls": usage["cacheHit"] = True
                if fault == "token_bool": usage["inputTokens"] = False
                if fault == "latency_nan": usage["elapsedMs"] = float("nan")
                self.write_inputs()
                with self.assertRaises(ValueError): self.evaluate()
                self.captures = original

    def test_numeric_usd_cost_uses_exact_decimal_and_unknown_call_keeps_total_null(self):
        for row, amount in zip(self.captures["captures"], (.1, .2, 0, 0)):
            row["capture"]["usage"].update(unit="usd", amount=amount)
        self.write_inputs()
        usage = self.evaluate()["usage"]
        self.assertEqual("usd", usage["unit"])
        self.assertEqual("0.3", usage["knownAmount"])
        self.assertEqual("0.3", usage["totalAmount"])
        self.captures["captures"][0]["capture"]["usage"]["unknownCalls"] = 1
        self.write_inputs()
        usage = self.evaluate()["usage"]
        self.assertEqual(0, usage["unknownAmountJobs"])
        self.assertEqual(1, usage["unknownCalls"])
        self.assertTrue(usage["hasUnknownCost"])
        self.assertIsNone(usage["totalAmount"])

    def test_nearest_rank_p95_excludes_cached_latency(self):
        usage = self.captures["captures"][0]["capture"]["usage"]
        rows = [{"usage": dict(usage, elapsedMs=value)} for value in range(1, 21)]
        rows.append({"usage": dict(usage, cacheHit=True, calls=0, amount=0, elapsedMs=1000)})
        result = EVALUATOR.usage_summary(rows)
        self.assertEqual(20, result["latencySamplesUncached"])
        self.assertEqual(19, result["p95ElapsedMsNearestRank"])
        self.assertEqual(10.5, result["medianElapsedMs"])

    def test_output_cannot_overwrite_input_and_private_root_blocks_escape(self):
        with self.assertRaisesRegex(ValueError, "output_must_not_overwrite_inputs"):
            EVALUATOR.evaluate(self.freeze_path, output_path=self.root / "captures.json")
        with self.assertRaisesRegex(ValueError, "private_input_or_output_required"):
            EVALUATOR.evaluate(self.freeze_path, private_root=self.root / "different-root")

    def test_cli_writes_private_report_once_and_rejects_prepared_before_inputs(self):
        private = self.root / ".local"
        private.mkdir()
        for path in list(self.root.glob("*.json")):
            path.replace(private / path.name)
        output = private / "report.json"
        argv = ["--freeze", str(private / "freeze.json"), "--output", str(output)]
        with patch.object(EVALUATOR, "ROOT", self.root), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, EVALUATOR.main(argv))
            original = output.read_bytes()
            self.assertEqual(2, EVALUATOR.main(argv))
            self.assertEqual(original, output.read_bytes())
            self.freeze["status"] = "prepared_not_final_frozen"
            (private / "freeze.json").write_text(json.dumps(self.freeze), encoding="utf-8")
            for name in ("dataset", "baseline", "captures"):
                (private / (name + ".json")).unlink()
            argv[-1] = str(private / "not-created.json")
            self.assertEqual(2, EVALUATOR.main(argv))
            self.assertFalse((private / "not-created.json").exists())


if __name__ == "__main__":
    unittest.main()
