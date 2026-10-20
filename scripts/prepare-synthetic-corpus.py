"""Register the checked-in smoke data only. No private directories or providers."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("corpus_gate", ROOT / "scripts/validate-evaluation-corpus.py")
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / ".local/evaluation/synthetic-corpus.json")
    args = parser.parse_args()
    output = args.output.resolve()
    if not any(output.is_relative_to(root) for root in (ROOT / ".local", ROOT / "project-docs/evaluation")):
        parser.error("Synthetic register output must be under .local or project-docs/evaluation")
    source = ROOT / "project-docs/evaluation/matching-baseline-smoke.json"
    if output == source:
        parser.error("Must not overwrite smoke source")
    dataset = json.loads(source.read_text(encoding="utf-8"))
    manifest = {"schema_version": 1, "corpus_id": "stage-a-synthetic-register-v1",
        "status": "synthetic_only_not_quality_evidence", "split_locked_at_utc": None,
        "source_artifact_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "payload_hash_convention": "sha256-json-utf8-sorted-keys-compact-no-ascii-escape",
        "documents": [], "field_labels": [], "pair_labels": []}
    for kind, records in (("cv", dataset["resumes"]), ("jd", dataset["jobs"])):
        for record in records:
            manifest["documents"].append({"document_id": record["id"], "group_id": record["groupId"],
                "kind": kind, "industry": "it", "language": record["language"], "format": "json", "layout": "inline_text",
                "sha256": GATE.canonical_hash(record), "hash_kind": "canonical_json_sha256", "synthetic": True,
                "private_source_ref": os.path.relpath(source, output.parent).replace("\\", "/"), "source_record_id": record["id"],
                "rights_status": "verified", "rights_evidence_ref": "project-authored synthetic smoke; no third-party CV/JD",
                "allowed_uses": ["local_evaluation"], "provider_transfer_allowed": False,
                "split": "smoke", "label_version": "synthetic-smoke-v1", "review_status": "synthetic_unreviewed"})
    groups = {doc["group_id"]: doc for doc in manifest["documents"]}
    for label in dataset["labels"]:
        resume, job = groups[label["resumeGroupId"]], groups[label["jobGroupId"]]
        manifest["pair_labels"].append({"resume_group_id": resume["group_id"], "job_group_id": job["group_id"],
            "resume_document_id": resume["document_id"], "job_document_id": job["document_id"],
            "resume_sha256": resume["sha256"], "job_sha256": job["sha256"], "split": "smoke", "relevance": label["relevance"],
            "requirements": [], "rationale": "Synthetic code-path label only; not human quality judgment",
            "label_version": "synthetic-smoke-v1", "review_status": "synthetic_unreviewed"})
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Registered 6 synthetic documents, 6 groups, 5 synthetic pairs; no quality evidence or provider calls.")


if __name__ == "__main__":
    main()
