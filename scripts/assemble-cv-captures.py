"""Assemble owner-authorized evaluation-capture exports for offline CV scoring.

Mapping JSON: [{"documentId":"pseudonymous-id","captureFile":"job-capture.json"}].
All inputs/output stay private in .local. No API, cookies, credentials or network.
"""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def assemble(dataset_path, mapping_path, output):
    private = (ROOT / '.local').resolve()
    dataset_path, mapping_path, output = (p.resolve() for p in (dataset_path, mapping_path, output))
    if not all(p.is_relative_to(private) for p in (dataset_path, mapping_path, output)) or output.exists():
        raise ValueError('private_paths_and_new_output_required')
    raw_dataset = dataset_path.read_bytes()
    dataset = json.loads(raw_dataset)
    docs = {doc['id']: doc for doc in dataset['documents']}
    mapping = json.loads(mapping_path.read_text(encoding='utf-8-sig'))
    shared_keys = ('contract', 'parser', 'provider', 'model', 'method', 'promptSha256', 'parameters', 'beforeUserCorrection', 'unit')
    shared, captures, seen = None, [], set()
    for item in mapping:
        identity = item['documentId']
        capture_path = (mapping_path.parent / item['captureFile']).resolve()
        if identity not in docs or identity in seen or not capture_path.is_relative_to(private):
            raise ValueError('capture_mapping_invalid')
        seen.add(identity)
        export = json.loads(capture_path.read_text(encoding='utf-8-sig'))
        current = {key: export[key] for key in shared_keys}
        if export['schemaVersion'] != 1 or not export['beforeUserCorrection'] or export['contract'] != 'cv-fields-v3':
            raise ValueError('original_full_cv_capture_required')
        if shared is not None and current != shared:
            raise ValueError('mixed_methods_require_separate_runs')
        shared = current
        doc = export['document']
        if doc['sourceSha256'] != docs[identity]['sha256']:
            raise ValueError('capture_source_mismatch')
        captures.append({**doc, 'id': identity})
    if not shared:
        raise ValueError('at_least_one_export_required')
    run = {'schemaVersion': 1, 'datasetSha256': hashlib.sha256(raw_dataset).hexdigest(), **shared, 'documents': captures}
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open('x', encoding='utf-8') as target:
        target.write(json.dumps(run, ensure_ascii=False, indent=2) + '\n')
    return {'captures': len(captures), 'missingCaptures': len(docs) - len(captures), 'providerCalled': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('dataset', type=Path)
    parser.add_argument('mapping', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = assemble(args.dataset, args.mapping, args.output)
    except (OSError, ValueError, KeyError, TypeError):
        print(json.dumps({'valid': False, 'error': 'capture_assembly_failed', 'providerCalled': False}))
        return 1
    print(json.dumps({'valid': True, **result}))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
