import contextlib
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("stage_d_freeze", ROOT / "scripts/check-stage-d-freeze.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Packet:
    """Artificial metadata/files only; no actual CV, model or provider is used."""
    def __init__(self, root):
        self.root = root
        self.paths = {}
        self.objects = {}
        for path, raw in (("src/method.cs", b"test source"), ("tests/method.cs", b"test assertion"),
                          ("build/api.dll", b"artificial assembly bytes"), ("build/tests.dll", b"artificial test assembly bytes")):
            self.write(path, raw)
        self.objects["source_manifest"] = {
            "schema_version": 1, "sourceFiles": [self.ref("src/method.cs")],
            "test_source_files": [self.ref("tests/method.cs")],
            "backend_assembly": self.ref("build/api.dll"), "test_assembly": self.ref("build/tests.dll")}
        source = self.objects["source_manifest"]
        source["source_bundle_sha256"] = hashlib.sha256(json.dumps(source["sourceFiles"], sort_keys=True, separators=(",", ":")).encode()).hexdigest()
        self.save("source_manifest")
        self.objects["method_inventory"] = {
            "schema_version": 1, "methods": {key: {"version": "toy-v1"} for key in MODULE.METHODS},
            "source_files": source["sourceFiles"], "assembly": source["backend_assembly"],
            "source_bundle_sha256": source["source_bundle_sha256"],
            "base_step5_manifest_sha256": self.ref(self.paths["source_manifest"])["sha256"]}
        docs = [self.doc("cv0", "c0", "cv"), self.doc("cv1", "c1", "cv"),
                self.doc("jd0", "j0", "jd"), self.doc("jd1", "j1", "jd"),
                self.doc("tuning0", "t0", "cv", "tuning")]
        self.objects["data_plan"] = {
            "schema_version": 1, "documents": docs,
            "final_document_ids": ["cv0", "cv1", "jd0", "jd1"], "tuning_group_ids": ["t0"],
            "final_pair_labels": [{"resume_group_id": cv, "job_group_id": jd, "relevance": 3}
                                  for cv in ("c0", "c1") for jd in ("j0", "j1")],
            "final_reference_labels_ready": True, "final_independence_verified": True, "owner_scope_approved": True}
        self.objects["budget_plan"] = {
            "schema_version": 1, "remaining_guarded_usd": "1.97", "proposed_cap_usd": "0.50",
            "maximum_combined_reservation_usd": "0.46448640",
            "search_plan": {"planned_credits": 4, "authorized_remaining_credits": 4}}
        self.bundle = {"schema_version": 1, "status": "final_frozen", "refs": {},
                       "readiness": {key: True for key in MODULE.GATES},
                       "execution": {"api_calls_authorized": False}, "provider_evidence": [], "evaluation_tools": []}
        for name in ("source_manifest", "method_inventory", "data_plan", "budget_plan"):
            self.refresh(name)
        self.paths["protocol"] = "meta/protocol.md"
        self.write(self.paths["protocol"], b"# Plain Markdown protocol\nNot JSON, never executable.\n")
        self.bundle["refs"]["protocol"] = self.ref(self.paths["protocol"])
        self.write("meta/parameters.json", b'{"schema_version":1,"purpose":"toy"}')
        self.bundle["method_parameters_plan"] = self.ref("meta/parameters.json")
        for path in ("tools/check.py", "tools/evaluate.py"):
            self.write(path, b"# artificial pinned tool source, never executed\n")
            self.bundle["evaluation_tools"].append(self.ref(path))
        for name in ("DeepSeek", "Tavily", "Resend"):
            path = "meta/provider-" + name + ".json"
            self.write(path, json.dumps({"provider": name, "historical": True}).encode())
            self.bundle["provider_evidence"].append(self.ref(path))

    @staticmethod
    def doc(identity, group, kind, split="final"):
        return {"document_id": identity, "group_id": group, "kind": kind, "split": split,
                "provider_transfer_allowed": True, "allowed_uses": ["local_evaluation", "provider_evaluation"]}

    def write(self, relative, raw):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)

    def ref(self, relative):
        return {"path": relative, "sha256": hashlib.sha256((self.root / relative).read_bytes()).hexdigest()}

    def save(self, name):
        self.paths[name] = "meta/" + name + ".json"
        self.write(self.paths[name], (json.dumps(self.objects[name]) + "\n").encode())

    def refresh(self, name):
        self.save(name)
        if hasattr(self, "bundle"):
            self.bundle["refs"][name] = self.ref(self.paths[name])

    def check(self):
        return MODULE.check(self.bundle, self.root)

    def prepared(self):
        self.bundle["status"] = "prepared_not_final_frozen"
        self.bundle["readiness"] = {key: False for key in MODULE.GATES}
        data = self.objects["data_plan"]
        for doc in data["documents"]:
            doc["provider_transfer_allowed"] = False
            doc["allowed_uses"] = ["local_evaluation"]
        for key in ("final_reference_labels_ready", "final_independence_verified", "owner_scope_approved"):
            data[key] = False
        data["final_pair_labels"] = 0
        self.objects["budget_plan"]["search_plan"]["authorized_remaining_credits"] = 0
        self.refresh("data_plan")
        self.refresh("budget_plan")


class FreezePreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="careergap-freeze-metadata-")
        self.addCleanup(self.temp.cleanup)
        self.packet = Packet(Path(self.temp.name))

    def test_duplicate_json_keys_and_nonfinite_constants_cannot_override_declarations(self):
        for raw in (b'{"provider_transfer_allowed":false,"provider_transfer_allowed":true}',
                    b'{"remaining_guarded_usd":NaN}', b'{"remaining_guarded_usd":Infinity}'):
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                MODULE._json(raw)

    def test_boolean_is_not_a_schema_version(self):
        self.packet.bundle["schema_version"] = True
        self.assertIn("bundle_schema", self.packet.check()["errors"])
        self.packet.bundle["schema_version"] = 1
        for name in ("source_manifest", "method_inventory", "data_plan", "budget_plan"):
            with self.subTest(name=name):
                original = self.packet.objects[name]["schema_version"]
                self.packet.objects[name]["schema_version"] = True
                self.packet.refresh(name)
                self.assertIn(name + "_schema", self.packet.check()["errors"])
                self.packet.objects[name]["schema_version"] = original
                self.packet.refresh(name)

    def test_consistent_toy_final_metadata_ready_never_authorizes_execution(self):
        result = self.packet.check()
        self.assertTrue(result["ready"], result)
        self.assertEqual(0, MODULE.exit_code(result))
        self.assertFalse(result["execution_authorized"])
        self.assertEqual(0, result["network_requests"])
        self.assertEqual(4, result["counts"]["final_labeled_group_pairs"])

    def test_current_prepared_shape_is_pending_not_invalid_or_authorized(self):
        self.packet.prepared()
        result = self.packet.check()
        self.assertTrue(result["valid"], result)
        self.assertFalse(result["ready"])
        self.assertEqual(2, MODULE.exit_code(result))
        self.assertIn("final_provider_rights_missing", result["pending"])
        self.assertIn("insufficient_search_allowance", result["pending"])
        self.assertFalse(result["execution_authorized"])

    def test_prepared_status_cannot_become_frozen_from_green_booleans(self):
        self.packet.bundle["status"] = "prepared_not_final_frozen"
        result = self.packet.check()
        self.assertEqual(["bundle_not_final_frozen"], result["pending"])
        self.assertEqual(2, MODULE.exit_code(result))

    def test_runtime_hash_drift_is_invalid(self):
        self.packet.write("src/method.cs", b"changed implementation")
        result = self.packet.check()
        self.assertEqual(1, MODULE.exit_code(result))
        self.assertIn("source_file:invalid:0", result["errors"])

    def test_plain_markdown_protocol_is_hash_checked_without_json_parsing(self):
        self.assertTrue(self.packet.check()["ready"])
        self.packet.write(self.packet.paths["protocol"], b"edited protocol")
        result = self.packet.check()
        self.assertIn("ref_invalid:protocol", result["errors"])

    def test_optional_parameters_and_required_final_tools_are_pinned(self):
        self.packet.write("meta/parameters.json", b"changed parameter plan")
        self.assertIn("method_parameters_plan_ref_invalid", self.packet.check()["errors"])
        del self.packet.bundle["method_parameters_plan"]
        self.packet.bundle["evaluation_tools"].pop()
        self.assertIn("final_frozen_requires_evaluation_tools", self.packet.check()["errors"])
        self.packet.bundle["status"] = "prepared_not_final_frozen"
        result = self.packet.check()
        self.assertTrue(result["valid"], result)
        self.assertIn("evaluation_tools_not_pinned", result["pending"])

    def test_json_plan_is_parsed_from_the_same_bytes_whose_hash_was_verified(self):
        data = self.packet.objects["data_plan"]
        data["documents"][0]["provider_transfer_allowed"] = False
        self.packet.refresh("data_plan")
        path = (self.packet.root / self.packet.paths["data_plan"]).resolve()
        good_hashed_bytes = path.read_bytes()
        forged = copy.deepcopy(data)
        forged["documents"][0]["provider_transfer_allowed"] = True
        forged_bytes = json.dumps(forged).encode()
        original_read = Path.read_bytes
        calls = []

        def changing_read(current):
            if current.resolve() == path:
                calls.append(True)
                return good_hashed_bytes if len(calls) == 1 else forged_bytes
            return original_read(current)

        with mock.patch.object(Path, "read_bytes", changing_read):
            result = self.packet.check()
        self.assertEqual(1, len(calls))
        self.assertIn("provider_rights_claim_conflict", result["errors"])
        self.assertFalse(result["ready"])

    def test_method_inventory_cannot_bind_to_another_source_manifest(self):
        self.packet.objects["method_inventory"]["base_step5_manifest_sha256"] = "a" * 64
        self.packet.refresh("method_inventory")
        self.assertIn("method_inventory_source_binding_mismatch", self.packet.check()["errors"])

    def test_group_split_leakage_and_wrong_final_ids_are_invalid(self):
        data = self.packet.objects["data_plan"]
        data["documents"].append(Packet.doc("leaked", "c0", "cv", "tuning"))
        data["tuning_group_ids"].append("c0")
        data["final_document_ids"].append("not-registered")
        self.packet.refresh("data_plan")
        errors = self.packet.check()["errors"]
        self.assertIn("group_split_leakage", errors)
        self.assertIn("final_document_inventory_mismatch", errors)

    def test_true_rights_gate_cannot_override_actual_transfer_false(self):
        self.packet.objects["data_plan"]["documents"][0]["provider_transfer_allowed"] = False
        self.packet.refresh("data_plan")
        self.assertIn("provider_rights_claim_conflict", self.packet.check()["errors"])

    def test_true_transfer_flag_alone_does_not_grant_provider_evaluation_use(self):
        self.packet.objects["data_plan"]["documents"][0]["allowed_uses"] = ["local_evaluation"]
        self.packet.refresh("data_plan")
        self.assertIn("provider_rights_claim_conflict", self.packet.check()["errors"])

    def test_true_scope_or_independence_gate_cannot_override_data_plan_false(self):
        for key in ("owner_scope_approved", "final_independence_verified"):
            with self.subTest(key=key):
                data = self.packet.objects["data_plan"]
                data[key] = False
                self.packet.refresh("data_plan")
                self.assertIn("data_plan_claim_conflict:" + key, self.packet.check()["errors"])
                data[key] = True
                self.packet.refresh("data_plan")

    def test_pair_labels_must_cover_unique_final_cv_job_groups(self):
        data = self.packet.objects["data_plan"]
        data["final_pair_labels"].pop()
        self.packet.refresh("data_plan")
        self.assertIn("final_reference_claim_incomplete_pairs", self.packet.check()["errors"])

    def test_unlabeled_pair_remains_unknown_and_count_cannot_prove_coverage(self):
        data = self.packet.objects["data_plan"]
        data["final_pair_labels"][0]["relevance"] = None
        self.packet.refresh("data_plan")
        result = self.packet.check()
        self.assertEqual(3, result["counts"]["final_labeled_group_pairs"])
        self.assertIn("final_reference_claim_incomplete_pairs", result["errors"])
        data["final_pair_labels"] = 4
        self.packet.refresh("data_plan")
        self.assertIn("final_pair_labels_detail_required", self.packet.check()["errors"])

    def test_money_comparison_is_decimal_and_reservation_cannot_exceed_cap(self):
        budget = self.packet.objects["budget_plan"]
        budget["maximum_combined_reservation_usd"] = "0.50000000000000000001"
        self.packet.refresh("budget_plan")
        self.assertIn("reservation_exceeds_proposed_cap", self.packet.check()["errors"])
        budget["maximum_combined_reservation_usd"] = "0.46448640"
        budget["remaining_guarded_usd"] = "0.49"
        self.packet.refresh("budget_plan")
        self.assertIn("insufficient_guarded_usd", self.packet.check()["pending"])

    def test_non_finite_or_negative_money_is_invalid(self):
        for value in ("NaN", "Infinity", "-0.01", True):
            with self.subTest(value=value):
                self.packet.objects["budget_plan"]["remaining_guarded_usd"] = value
                self.packet.refresh("budget_plan")
                self.assertIn("budget_money_invalid", self.packet.check()["errors"])

    def test_search_approval_cannot_override_zero_authorized_credits(self):
        self.packet.objects["budget_plan"]["search_plan"]["authorized_remaining_credits"] = 0
        self.packet.refresh("budget_plan")
        self.assertIn("search_allowance_claim_conflict", self.packet.check()["errors"])

    def test_current_funds_reconciliation_is_a_required_gate(self):
        self.packet.bundle["status"] = "prepared_not_final_frozen"
        self.packet.bundle["readiness"]["funds_reconciled"] = False
        self.assertIn("gate_pending:funds_reconciled", self.packet.check()["pending"])

    def test_no_true_gate_or_execution_flag_coercion(self):
        self.packet.bundle["readiness"]["owner_scope_approved"] = "true"
        self.packet.bundle["execution"]["api_calls_authorized"] = True
        result = self.packet.check()
        self.assertIn("readiness_boolean_required:owner_scope_approved", result["errors"])
        self.assertIn("execution_authority_forbidden", result["errors"])
        self.assertFalse(result["execution_authorized"])

    def test_path_escape_and_secret_file_references_rejected(self):
        for name in ("../outside.json", "C:/outside.json", "https://example.invalid/value", "config/.env"):
            with self.subTest(name=name):
                self.packet.bundle["refs"]["protocol"] = {"path": name, "sha256": "a" * 64}
                self.assertIn("ref_invalid:protocol", self.packet.check()["errors"])

    def test_cli_exit_codes_and_no_sensitive_value_output(self):
        for expected in (0, 2, 1):
            if expected == 2:
                self.packet.prepared()
            elif expected == 1:
                self.packet.bundle["Email__ApiKey"] = "DO_NOT_PRINT_THIS_VALUE"
            path = self.packet.root / "bundle.json"
            path.write_text(json.dumps(self.packet.bundle), encoding="utf-8")
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                code = MODULE.main([str(path)], root=self.packet.root)
            self.assertEqual(expected, code)
            self.assertNotIn("DO_NOT_PRINT_THIS_VALUE", output.getvalue())
            self.assertEqual(0, json.loads(output.getvalue())["provider_calls"])


if __name__ == "__main__":
    unittest.main()
