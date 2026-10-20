import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("corpus_gate", ROOT / "scripts/validate-evaluation-corpus.py")
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class CorpusSafetyTests(unittest.TestCase):
    def setUp(self):
        self.manifest = json.loads((ROOT / "project-docs/evaluation/corpus-register-2026-10-16.json").read_text(encoding="utf-8"))
        self.base = ROOT / "project-docs/evaluation"

    def test_synthetic_is_valid_but_never_quality_evidence(self):
        result = GATE.validate(self.manifest, self.base, verify_files=True)
        self.assertTrue(result["valid"], result["errors"])
        self.assertIn("synthetic_only_not_quality_evidence", result["warnings"])
        self.assertFalse(result["provider_called"])

    def test_same_origin_cannot_cross_tuning_and_final(self):
        self.manifest["split_locked_at_utc"] = "2026-10-16T00:00:00Z"
        first = self.manifest["documents"][0]
        first["split"] = "tuning"
        variant = copy.deepcopy(first)
        variant.update(document_id="variant", split="final", sha256="a" * 64)
        self.manifest["documents"].append(variant)
        self.assertTrue(any("group_split_leakage" in e for e in GATE.validate(self.manifest, self.base)["errors"]))

    def test_duplicate_bytes_cannot_be_disguised_as_new_group(self):
        self.manifest["documents"][1]["sha256"] = self.manifest["documents"][0]["sha256"]
        self.assertTrue(any("duplicate_content_group_or_split" in e for e in GATE.validate(self.manifest, self.base)["errors"]))

    def test_permission_and_provider_transfer_fail_closed(self):
        self.manifest["documents"][0]["rights_status"] = "pending"
        result = GATE.validate(self.manifest, self.base, for_provider=True)
        self.assertTrue(any("rights_not_verified" in e for e in result["errors"]))
        self.assertTrue(any("provider_transfer_not_allowed" in e for e in result["errors"]))

    def test_tampered_hash_is_rejected(self):
        self.manifest["documents"][0]["sha256"] = "a" * 64
        result = GATE.validate(self.manifest, self.base, verify_files=True)
        self.assertTrue(any("hash_mismatch" in e for e in result["errors"]))

    def test_cross_split_pair_and_stale_document_reference_rejected(self):
        self.manifest["documents"][0]["split"] = "demo"
        self.manifest["pair_labels"][0]["resume_sha256"] = "f" * 64
        errors = GATE.validate(self.manifest, self.base)["errors"]
        self.assertTrue(any("cross_split_pair" in e for e in errors))
        self.assertTrue(any("document_or_hash" in e for e in errors))

    def test_unknown_pairs_are_not_negative_labels(self):
        self.manifest["pair_labels"][0]["relevance"] = None
        result = GATE.validate(self.manifest, self.base)
        self.assertTrue(result["valid"])
        self.assertIn("pair_label[0]:unlabeled_unknown", result["warnings"])

    def test_user_statement_cannot_be_extraction_ground_truth(self):
        doc = self.manifest["documents"][0]
        self.manifest["field_labels"] = [{"document_id": doc["document_id"], "document_sha256": doc["sha256"],
            "field_path": "skills[0]", "value": "React", "status": "present", "provenance": "user_statement",
            "locator": {}, "review_status": "pending", "label_version": "v1"}]
        self.assertIn("field_label[0]:source_evidence_required", GATE.validate(self.manifest, self.base)["errors"])

    def test_external_jsonl_labels_are_read_without_echoing_content(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "pairs.jsonl").write_text(json.dumps(self.manifest.pop("pair_labels")[0]) + "\n", encoding="utf-8")
            self.manifest["pair_labels_ref"] = "pairs.jsonl"
            result = GATE.validate(self.manifest, path)
            self.assertTrue(result["valid"])
            self.assertEqual(result["pair_labels"], 1)
            self.assertNotIn("rationale", json.dumps(result))


if __name__ == "__main__":
    unittest.main()
