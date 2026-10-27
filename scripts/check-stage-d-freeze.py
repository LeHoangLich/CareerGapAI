"""Offline stage-D freeze preflight: metadata and explicit hashes, never API authority.

Exit 0 means declared final metadata is consistent, 2 means prepared/pending,
and 1 means invalid or contradictory metadata. No corpus text, label-value
files, secrets, providers, databases, or directory sweeps are opened.
"""
import argparse
import hashlib
import json
import re
from decimal import Decimal, InvalidOperation
from pathlib import Path, PureWindowsPath


ROOT = Path(__file__).resolve().parents[1]
REF_NAMES = ("source_manifest", "method_inventory", "protocol", "data_plan", "budget_plan")
GATES = ("owner_scope_approved", "final_dataset_frozen", "final_independence_verified",
         "provider_rights_verified", "final_reference_labels_ready",
         "required_search_allowance_approved", "funds_reconciled")
METHODS = {"document_parser", "cv_extraction", "jd_extraction", "baseline", "revision_projection",
           "pair", "writing", "learning", "template", "llm_transport"}
SHA = re.compile(r"[0-9a-f]{64}\Z")


def _secret_fields(value):
    if isinstance(value, dict):
        for key, item in value.items():
            normalized = re.sub(r"[^a-z]", "", str(key).casefold())
            if normalized in {"apikey", "password", "secret", "token", "accesstoken", "refreshtoken",
                              "authorization", "cookie", "resettoken", "resetlink"} or normalized.endswith(("apikey", "password")):
                raise ValueError("secret_field_forbidden")
            _secret_fields(item)
    elif isinstance(value, list):
        for item in value:
            _secret_fields(item)


def _path(root, name):
    if not isinstance(name, str) or not name or "\0" in name or "://" in name:
        raise ValueError("explicit_relative_path_required")
    windows = PureWindowsPath(name)
    normalized = name.replace("\\", "/")
    parts = Path(normalized).parts
    if windows.drive or windows.is_absolute() or Path(normalized).is_absolute() or ".." in parts:
        raise ValueError("path_outside_workspace")
    lowered = [part.casefold() for part in parts]
    if any(part == ".env" or part.startswith(".env.") and part != ".env.example" or
           part in {"appsettings.local.json", "credentials", "credentials.json"} or
           part.endswith((".pfx", ".p12", ".pem", ".key")) for part in lowered):
        raise ValueError("secret_path_forbidden")
    resolved = (root / normalized).resolve()
    if not resolved.is_relative_to(root.resolve()):
        raise ValueError("path_outside_workspace")
    return resolved


def _json(raw):
    def unique_members(pairs):
        result = {}
        for key, item in pairs:
            if key in result:
                raise ValueError("duplicate_json_key")
            result[key] = item
        return result

    def reject_constant(_):
        raise ValueError("nonfinite_json_number")

    value = json.loads(raw.decode("utf-8-sig"), parse_float=Decimal,
                       parse_constant=reject_constant, object_pairs_hook=unique_members)
    _secret_fields(value)
    return value


def _amount(value):
    if isinstance(value, bool) or isinstance(value, float) or not isinstance(value, (str, int, Decimal)):
        raise ValueError("invalid_money")
    try:
        amount = Decimal(value)
    except InvalidOperation as error:
        raise ValueError("invalid_money") from error
    if not amount.is_finite() or amount < 0:
        raise ValueError("invalid_money")
    return amount


def check(bundle, root=ROOT):
    """Return only status/counters/codes. root is injectable for isolated tests."""
    root = Path(root).resolve()
    errors, pending = [], []
    counts = {"references_verified": 0, "provider_evidence_verified": 0,
              "evaluation_tool_refs_verified": 0, "method_parameters_ref_verified": 0,
              "source_files_verified": 0, "test_source_files_verified": 0,
              "method_source_files_verified": 0, "assemblies_verified": 0,
              "documents": 0, "final_documents": 0, "final_cv_groups": 0,
              "final_jd_groups": 0, "final_labeled_group_pairs": 0}

    def result():
        bad = sorted(set(errors))
        waiting = sorted(set(pending))
        ready = not bad and not waiting
        return {"valid": not bad, "ready": ready,
                "status": "invalid" if bad else "ready_metadata_only" if ready else "pending",
                "errors": bad, "pending": waiting, "counts": counts,
                "network_requests": 0, "provider_calls": 0, "execution_authorized": False,
                "proof_scope": "Explicit local hashes and declared metadata only; no rights audit, quality acceptance or API authorization."}

    if not isinstance(bundle, dict):
        errors.append("bundle_schema")
        return result()
    try:
        _secret_fields(bundle)
    except ValueError as error:
        errors.append(str(error))
        return result()
    if type(bundle.get("schema_version")) is not int or bundle["schema_version"] != 1:
        errors.append("bundle_schema")
    if bundle.get("status") not in {"prepared_not_final_frozen", "final_frozen"}:
        errors.append("bundle_status")
    if bundle.get("status") != "final_frozen":
        pending.append("bundle_not_final_frozen")
    readiness = bundle.get("readiness", {})
    if not isinstance(readiness, dict):
        readiness = {}
        errors.append("readiness_schema")
    for gate in GATES:
        if type(readiness.get(gate)) is not bool:
            errors.append("readiness_boolean_required:" + gate)
        elif readiness[gate] is False:
            pending.append("gate_pending:" + gate)
    execution = bundle.get("execution", {})
    if not isinstance(execution, dict) or execution.get("api_calls_authorized") is not False:
        errors.append("execution_authority_forbidden")

    def ref(entry, code, parse=True):
        try:
            if not isinstance(entry, dict) or not SHA.fullmatch(str(entry.get("sha256", ""))):
                raise ValueError("reference_schema")
            path = _path(root, entry.get("path"))
            raw = path.read_bytes()
            if hashlib.sha256(raw).hexdigest() != entry["sha256"]:
                raise ValueError("hash_mismatch")
            return _json(raw) if parse else True
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError, TypeError):
            errors.append(code)
            return None

    refs = bundle.get("refs", {})
    if not isinstance(refs, dict):
        refs = {}
        errors.append("refs_schema")
    loaded = {}
    for name in REF_NAMES:
        value = ref(refs.get(name), "ref_invalid:" + name, parse=name != "protocol")
        if value is not None:
            loaded[name] = value
            counts["references_verified"] += 1
    evidence = bundle.get("provider_evidence")
    if not isinstance(evidence, list) or len(evidence) != 3:
        errors.append("provider_evidence_three_refs_required")
        evidence = []
    for index, item in enumerate(evidence):
        if ref(item, "provider_evidence_invalid:" + str(index), parse=False):
            counts["provider_evidence_verified"] += 1
    if "method_parameters_plan" in bundle:
        if ref(bundle["method_parameters_plan"], "method_parameters_plan_ref_invalid", parse=False):
            counts["method_parameters_ref_verified"] = 1
    tools = bundle.get("evaluation_tools", [])
    if not isinstance(tools, list):
        errors.append("evaluation_tools_schema")
        tools = []
    tool_paths = set()
    for index, item in enumerate(tools):
        name = item.get("path") if isinstance(item, dict) else None
        if not isinstance(name, str) or name.replace("\\", "/") in tool_paths:
            errors.append("evaluation_tool_duplicate_or_path")
            continue
        tool_paths.add(name.replace("\\", "/"))
        if ref(item, "evaluation_tool_invalid:" + str(index), parse=False):
            counts["evaluation_tool_refs_verified"] += 1
    if counts["evaluation_tool_refs_verified"] < 2:
        pending.append("evaluation_tools_not_pinned")
        if bundle.get("status") == "final_frozen":
            errors.append("final_frozen_requires_evaluation_tools")

    def file_list(items, count_key, code):
        if not isinstance(items, list) or not items:
            errors.append(code + ":list_required")
            return
        seen = set()
        for index, item in enumerate(items):
            name = item.get("path") if isinstance(item, dict) else None
            if not isinstance(name, str):
                errors.append(code + ":invalid:" + str(index))
                continue
            if name in seen:
                errors.append(code + ":duplicate")
                continue
            if isinstance(name, str):
                seen.add(name)
            if ref(item, code + ":invalid:" + str(index), parse=False):
                counts[count_key] += 1

    source = loaded.get("source_manifest")
    if source is not None:
        if not isinstance(source, dict) or type(source.get("schema_version")) is not int or source["schema_version"] != 1:
            errors.append("source_manifest_schema")
            source = None
        else:
            file_list(source.get("sourceFiles"), "source_files_verified", "source_file")
            file_list(source.get("test_source_files"), "test_source_files_verified", "test_source_file")
            for name in ("backend_assembly", "test_assembly"):
                if ref(source.get(name), "assembly_invalid:" + name, parse=False):
                    counts["assemblies_verified"] += 1
            files = source.get("sourceFiles")
            if isinstance(files, list):
                try:
                    actual_bundle_hash = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
                except TypeError:
                    actual_bundle_hash = None
                if actual_bundle_hash is None or source.get("source_bundle_sha256") != actual_bundle_hash:
                    errors.append("source_bundle_hash_mismatch")
    inventory = loaded.get("method_inventory")
    if inventory is not None:
        if not isinstance(inventory, dict) or type(inventory.get("schema_version")) is not int or inventory["schema_version"] != 1:
            errors.append("method_inventory_schema")
        else:
            methods = inventory.get("methods")
            if not isinstance(methods, dict) or not METHODS.issubset(methods):
                errors.append("method_inventory_methods_incomplete")
            file_list(inventory.get("source_files"), "method_source_files_verified", "method_source_file")
            if source is not None and (
                inventory.get("base_step5_manifest_sha256") != refs["source_manifest"]["sha256"] or
                inventory.get("source_bundle_sha256") != source.get("source_bundle_sha256") or
                inventory.get("assembly") != source.get("backend_assembly")
            ):
                errors.append("method_inventory_source_binding_mismatch")

    data = loaded.get("data_plan")
    if data is not None:
        if not isinstance(data, dict) or type(data.get("schema_version")) is not int or data["schema_version"] != 1:
            errors.append("data_plan_schema")
        else:
            docs = data.get("documents")
            final_ids = data.get("final_document_ids")
            tuning_groups = data.get("tuning_group_ids")
            if not isinstance(docs, list) or not isinstance(final_ids, list) or not isinstance(tuning_groups, list):
                errors.append("data_plan_arrays_required")
            else:
                ids, group_splits = {}, {}
                for doc in docs:
                    if not isinstance(doc, dict) or not all(isinstance(doc.get(k), str) and doc[k] for k in ("document_id", "group_id")):
                        errors.append("document_identity")
                        continue
                    identity, group, split = doc["document_id"], doc["group_id"], doc.get("split")
                    if identity in ids:
                        errors.append("duplicate_document_id")
                    ids[identity] = doc
                    if split not in {"tuning", "final", "demo", "smoke"} or doc.get("kind") not in {"cv", "jd"}:
                        errors.append("document_kind_or_split")
                    if group in group_splits and group_splits[group] != split:
                        errors.append("group_split_leakage")
                    group_splits[group] = split
                    if type(doc.get("provider_transfer_allowed")) is not bool or not isinstance(doc.get("allowed_uses"), list) or "local_evaluation" not in doc["allowed_uses"]:
                        errors.append("document_rights_metadata")
                counts["documents"] = len(ids)
                if any(not isinstance(x, str) for x in final_ids + tuning_groups):
                    errors.append("data_plan_group_or_id_type")
                    selected = []
                else:
                    if len(set(final_ids)) != len(final_ids) or len(set(tuning_groups)) != len(tuning_groups):
                        errors.append("data_plan_duplicate_selection")
                    if set(tuning_groups) != {d["group_id"] for d in ids.values() if d.get("split") == "tuning"}:
                        errors.append("tuning_group_inventory_mismatch")
                    selected = [ids[x] for x in final_ids if x in ids]
                    if len(selected) != len(final_ids) or any(d.get("split") != "final" for d in selected):
                        errors.append("final_document_inventory_mismatch")
                    if {d["group_id"] for d in selected} & set(tuning_groups):
                        errors.append("final_tuning_group_overlap")
                counts["final_documents"] = len(selected)
                cv_groups = {d["group_id"] for d in selected if d.get("kind") == "cv"}
                jd_groups = {d["group_id"] for d in selected if d.get("kind") == "jd"}
                counts["final_cv_groups"], counts["final_jd_groups"] = len(cv_groups), len(jd_groups)
                if not cv_groups or not jd_groups:
                    pending.append("final_cv_or_jd_missing")
                for gate in ("owner_scope_approved", "final_independence_verified", "final_reference_labels_ready"):
                    if type(data.get(gate)) is not bool:
                        errors.append("data_plan_boolean_required:" + gate)
                    elif readiness.get(gate) is True and data[gate] is not True:
                        errors.append("data_plan_claim_conflict:" + gate)
                    elif data[gate] is not True:
                        pending.append("data_pending:" + gate)
                rights = bool(selected) and all(d.get("provider_transfer_allowed") is True and
                    isinstance(d.get("allowed_uses"), list) and "provider_evaluation" in d["allowed_uses"] for d in selected)
                if not rights:
                    pending.append("final_provider_rights_missing")
                    if readiness.get("provider_rights_verified") is True:
                        errors.append("provider_rights_claim_conflict")
                pair_rows = data.get("final_pair_labels")
                known = set()
                if type(pair_rows) is int and pair_rows == 0:
                    pending.append("final_pair_labels_missing")
                elif isinstance(pair_rows, list):
                    all_pairs = set()
                    for row in pair_rows:
                        if not isinstance(row, dict):
                            errors.append("final_pair_label_schema")
                            continue
                        key = (row.get("resume_group_id"), row.get("job_group_id"))
                        if any(not isinstance(part, str) for part in key):
                            errors.append("final_pair_label_group_or_duplicate")
                            continue
                        if key[0] not in cv_groups or key[1] not in jd_groups or key in all_pairs:
                            errors.append("final_pair_label_group_or_duplicate")
                        all_pairs.add(key)
                        relevance = row.get("relevance")
                        if relevance is not None and (type(relevance) is not int or relevance not in range(4)):
                            errors.append("final_pair_label_relevance")
                        elif relevance is not None:
                            known.add(key)
                else:
                    errors.append("final_pair_labels_detail_required")
                expected = {(cv, jd) for cv in cv_groups for jd in jd_groups}
                counts["final_labeled_group_pairs"] = len(known)
                if not expected or known != expected:
                    pending.append("final_pair_label_coverage_incomplete")
                    if readiness.get("final_reference_labels_ready") is True or data.get("final_reference_labels_ready") is True:
                        errors.append("final_reference_claim_incomplete_pairs")

    budget = loaded.get("budget_plan")
    if budget is not None:
        if not isinstance(budget, dict) or type(budget.get("schema_version")) is not int or budget["schema_version"] != 1:
            errors.append("budget_plan_schema")
        else:
            try:
                remaining = _amount(budget.get("remaining_guarded_usd"))
                proposed = _amount(budget.get("proposed_cap_usd"))
                reservation = _amount(budget.get("maximum_combined_reservation_usd"))
                if reservation > proposed:
                    errors.append("reservation_exceeds_proposed_cap")
                if proposed > remaining:
                    pending.append("insufficient_guarded_usd")
            except ValueError:
                errors.append("budget_money_invalid")
            search = budget.get("search_plan", {})
            if not isinstance(search, dict) or any(type(search.get(k)) is not int or search[k] < 0
                    for k in ("planned_credits", "authorized_remaining_credits")):
                errors.append("search_budget_invalid")
            elif search["planned_credits"] > search["authorized_remaining_credits"]:
                pending.append("insufficient_search_allowance")
                if readiness.get("required_search_allowance_approved") is True:
                    errors.append("search_allowance_claim_conflict")
            if "ledger_ref" in budget:
                ref(budget["ledger_ref"], "budget_ledger_ref_invalid", parse=False)
    if bundle.get("status") == "final_frozen" and pending:
        errors.append("final_frozen_with_pending_gates")
    return result()


def exit_code(result):
    return 1 if not result["valid"] else 0 if result["ready"] else 2


def main(argv=None, root=ROOT):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    args = parser.parse_args(argv)
    try:
        path = args.manifest.resolve()
        if not path.is_relative_to(Path(root).resolve()):
            raise ValueError("manifest_outside_workspace")
        relative = str(path.relative_to(Path(root).resolve()))
        path = _path(Path(root), relative)
        result = check(_json(path.read_bytes()), root)
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError, TypeError):
        result = {"valid": False, "ready": False, "status": "invalid", "errors": ["manifest_invalid_or_secret_field"],
                  "pending": [], "counts": {}, "network_requests": 0, "provider_calls": 0,
                  "execution_authorized": False}
    print(json.dumps(result, ensure_ascii=True))
    return exit_code(result)


if __name__ == "__main__":
    raise SystemExit(main())
