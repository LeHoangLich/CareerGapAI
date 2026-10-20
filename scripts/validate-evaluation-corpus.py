"""Offline corpus gate. Python standard library only; never calls a provider.

Reports counts/error codes, never document text, paths, quotes, or credentials.
Rights declarations require human evidence review; this cannot grant permission.
"""
import argparse
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SPLITS = {"smoke", "demo", "tuning", "final"}


def canonical_hash(record):
    raw = json.dumps(record, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def validate(manifest, base, verify_files=False, for_provider=False):
    errors, warnings = [], []
    if not isinstance(manifest, dict) or manifest.get("schema_version") != 1:
        return {"valid": False, "errors": ["manifest_schema"], "warnings": [], "documents": 0}
    documents = manifest.get("documents")
    if not isinstance(documents, list) or not documents:
        return {"valid": False, "errors": ["documents_required"], "warnings": [], "documents": 0}
    ids, groups, hashes = {}, {}, {}
    counts = {split: 0 for split in sorted(SPLITS)}
    for index, doc in enumerate(documents):
        prefix = f"document[{index}]"
        if not isinstance(doc, dict):
            errors.append(prefix + ":schema")
            continue
        identity, group, split = (doc.get(key) for key in ("document_id", "group_id", "split"))
        if not all(isinstance(value, str) and value.strip() for value in (identity, group, split)):
            errors.append(prefix + ":identity")
            continue
        if identity in ids:
            errors.append(prefix + ":duplicate_id")
        ids[identity] = doc
        if split not in SPLITS:
            errors.append(prefix + ":split")
        else:
            counts[split] += 1
        if group in groups and groups[group] != split:
            errors.append(prefix + ":group_split_leakage")
        groups[group] = split
        digest = doc.get("sha256", "")
        if not isinstance(digest, str) or len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
            errors.append(prefix + ":sha256")
        elif digest in hashes and hashes[digest] != (group, split):
            errors.append(prefix + ":duplicate_content_group_or_split")
        else:
            hashes[digest] = (group, split)
        if doc.get("kind") not in {"cv", "jd"} or doc.get("language") not in {"vi", "en", "mixed", "unknown"}:
            errors.append(prefix + ":kind_or_language")
        if doc.get("format") not in {"pdf", "docx", "text", "json"} or not doc.get("layout"):
            errors.append(prefix + ":format_or_layout")
        uses = doc.get("allowed_uses", [])
        if doc.get("rights_status") != "verified" or not doc.get("rights_evidence_ref") or not isinstance(uses, list) or "local_evaluation" not in uses:
            errors.append(prefix + ":rights_not_verified")
        if not isinstance(doc.get("synthetic"), bool):
            errors.append(prefix + ":synthetic_flag")
        if for_provider and (doc.get("provider_transfer_allowed") is not True or "provider_evaluation" not in uses):
            errors.append(prefix + ":provider_transfer_not_allowed")
        if split in {"tuning", "final"} and not manifest.get("split_locked_at_utc"):
            errors.append(prefix + ":split_not_locked")
        if doc.get("hash_kind") not in {"file_sha256", "canonical_json_sha256"}:
            errors.append(prefix + ":hash_kind")
        if verify_files:
            try:
                # Only explicit manifest references are opened, never directory sweeps.
                source = (base / doc["private_source_ref"]).resolve()
                raw = source.read_bytes()
                actual = hashlib.sha256(raw).hexdigest()
                if doc["hash_kind"] == "canonical_json_sha256":
                    dataset = json.loads(raw)
                    records = dataset["resumes" if doc["kind"] == "cv" else "jobs"]
                    record = [item for item in records if item["id"] == doc["source_record_id"]]
                    if len(record) != 1:
                        raise ValueError("record")
                    actual = canonical_hash(record[0])
                if actual != digest:
                    errors.append(prefix + ":hash_mismatch")
            except (OSError, ValueError, KeyError, TypeError):
                errors.append(prefix + ":source_unavailable_or_invalid")

    def labels(name):
        result = manifest.get(name, [])
        ref = manifest.get(name + "_ref")
        if ref:
            try:
                if result:
                    errors.append(name + ":ambiguous_inline_and_reference")
                result = [json.loads(line) for line in (base / ref).read_text(encoding="utf-8-sig").splitlines() if line.strip()]
            except (OSError, ValueError, TypeError):
                errors.append(name + ":unavailable_or_invalid")
                return []
        if not isinstance(result, list):
            errors.append(name + ":schema")
            return []
        return result

    field_labels, pair_labels = labels("field_labels"), labels("pair_labels")
    seen_fields, seen_pairs = set(), set()
    for index, label in enumerate(field_labels):
        prefix = f"field_label[{index}]"
        if not isinstance(label, dict):
            errors.append(prefix + ":schema")
            continue
        doc = ids.get(label.get("document_id"))
        if doc is None or label.get("document_sha256") != doc.get("sha256"):
            errors.append(prefix + ":document_or_hash")
        key = (label.get("document_id"), label.get("field_path"))
        if not all(isinstance(value, str) and value for value in key) or key in seen_fields:
            errors.append(prefix + ":field_or_duplicate")
        seen_fields.add(key)
        status, provenance = label.get("status"), label.get("provenance")
        if status not in {"present", "absent", "unreadable", "ambiguous"}:
            errors.append(prefix + ":status")
        locator = label.get("locator")
        if not isinstance(locator, dict):
            locator = {}
        if provenance == "user_statement" or (status == "present" and (provenance != "document_span" or not locator.get("quote") or not (locator.get("page") or locator.get("paragraph")))):
            errors.append(prefix + ":source_evidence_required")
        if status == "present" and label.get("value") is None:
            errors.append(prefix + ":value_required")
        if label.get("review_status") not in {"pending", "self_reviewed", "reviewed"} or not label.get("label_version"):
            errors.append(prefix + ":label_metadata")

    for index, label in enumerate(pair_labels):
        prefix = f"pair_label[{index}]"
        if not isinstance(label, dict):
            errors.append(prefix + ":schema")
            continue
        resume, job = ids.get(label.get("resume_document_id")), ids.get(label.get("job_document_id"))
        if resume is None or job is None:
            errors.append(prefix + ":unknown_document")
            continue
        key = (resume["group_id"], job["group_id"])
        if key in seen_pairs:
            errors.append(prefix + ":duplicate_group_pair")
        seen_pairs.add(key)
        if resume.get("kind") != "cv" or job.get("kind") != "jd":
            errors.append(prefix + ":kind")
        if resume["split"] != job["split"] or label.get("split") != resume["split"]:
            errors.append(prefix + ":cross_split_pair")
        for name, doc in (("resume", resume), ("job", job)):
            if label.get(name + "_group_id") != doc["group_id"] or label.get(name + "_sha256") != doc["sha256"]:
                errors.append(prefix + ":document_or_hash")
        relevance = label.get("relevance")
        if relevance is not None and (type(relevance) is not int or relevance not in range(4)):
            errors.append(prefix + ":relevance")
        if not label.get("label_version") or label.get("review_status") not in {"pending", "self_reviewed", "reviewed", "synthetic_unreviewed"}:
            errors.append(prefix + ":label_metadata")
        if relevance is None:
            warnings.append(prefix + ":unlabeled_unknown")
    synthetic = sum(doc.get("synthetic") is True for doc in ids.values())
    if synthetic == len(documents):
        warnings.append("synthetic_only_not_quality_evidence")
    return {"valid": not errors, "errors": errors, "warnings": warnings, "documents": len(documents),
            "groups": len(groups), "splits": counts, "synthetic_documents": synthetic,
            "field_labels": len(field_labels), "pair_labels": len(pair_labels),
            "file_hashes_verified": verify_files, "provider_rights_checked": for_provider,
            "provider_called": False, "rights_evidence_human_review_required": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--verify-files", action="store_true")
    parser.add_argument("--for-provider", action="store_true", help="Check declared rights only; no call or payment")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        manifest = json.loads(args.manifest.read_text(encoding="utf-8-sig"))
        result = validate(manifest, args.manifest.resolve().parent, args.verify_files, args.for_provider)
    except (OSError, ValueError, TypeError):
        result = {"valid": False, "errors": ["manifest_unavailable_or_invalid"], "provider_called": False}
    if args.output:
        output = args.output.resolve()
        if not output.is_relative_to(ROOT / ".local") or output == args.manifest.resolve():
            parser.error("Output must be under repository .local and must not overwrite input")
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))
    return 0 if result["valid"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
