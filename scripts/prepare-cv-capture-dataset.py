"""Prepare reviewed CV labels for OFFLINE replay. No network, provider key, or dispatch.

Uses the existing corpus manifest/rights/group/hash validator. Output contains private
field values and quotes and must remain in repository .local. Rights are declarations.
"""
import argparse
import importlib.util
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('corpus_validator', ROOT / 'scripts/validate-evaluation-corpus.py')
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


def prepare(manifest_path, split, output):
    private = (ROOT / '.local').resolve()
    manifest_path, output = manifest_path.resolve(), output.resolve()
    if not manifest_path.is_relative_to(private) or not output.is_relative_to(private):
        raise ValueError('private_input_and_output_required')
    if output.exists():
        raise ValueError('output_already_exists')
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    # Bound explicit references BEFORE the shared validator opens sources or JSONL labels.
    for doc in manifest.get('documents', []):
        source = (manifest_path.parent / doc['private_source_ref']).resolve()
        if not source.is_relative_to(private) or source.stat().st_size > 8_000_000:
            raise ValueError('private_bounded_source_required')
    for key in ('field_labels_ref', 'pair_labels_ref'):
        if manifest.get(key):
            label_file = (manifest_path.parent / manifest[key]).resolve()
            if not label_file.is_relative_to(private) or label_file.stat().st_size > 8_000_000:
                raise ValueError('private_bounded_labels_required')
    checked = validator.validate(manifest, manifest_path.parent, verify_files=True, for_provider=False)
    if not checked['valid']:
        raise ValueError('corpus_validation_failed:' + ','.join(checked['errors']))
    labels = manifest.get('field_labels', [])
    if manifest.get('field_labels_ref'):
        labels = [json.loads(line) for line in (manifest_path.parent / manifest['field_labels_ref']).read_text(encoding='utf-8-sig').splitlines() if line.strip()]
    documents = []
    for doc in manifest['documents']:
        if doc['kind'] != 'cv' or doc['split'] != split:
            continue
        source = (manifest_path.parent / doc['private_source_ref']).resolve()
        if doc['hash_kind'] != 'file_sha256' or doc['format'] not in {'pdf', 'docx'} or not source.is_relative_to(private):
            raise ValueError('private_cv_original_file_required')
        documents.append({
            'id': doc['document_id'], 'groupId': doc['group_id'], 'language': doc['language'],
            'format': doc['format'], 'layout': doc['layout'], 'file': os.path.relpath(source, output.parent),
            'sha256': doc['sha256'], 'synthetic': doc['synthetic'], 'localEvaluationAllowed': True,
            'rightsEvidenceRef': doc['rights_evidence_ref'],
            'labels': [{'path': label['field_path'], 'value': label.get('value'), 'status': label['status'],
                        'quote': label.get('locator', {}).get('quote'), 'reviewStatus': label['review_status']}
                       for label in labels if label['document_id'] == doc['document_id']]
        })
    if not documents:
        raise ValueError('selected_split_has_no_cv')
    dataset = {'schemaVersion': 1, 'datasetId': manifest['corpus_id'], 'split': split,
               'splitLockedAtUtc': manifest.get('split_locked_at_utc'), 'documents': documents}
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open('x', encoding='utf-8') as target:
        target.write(json.dumps(dataset, ensure_ascii=False, indent=2) + '\n')
    return {'documents': len(documents), 'groups': len({doc['groupId'] for doc in documents}),
            'providerCalled': False, 'syntheticOnly': all(doc['synthetic'] for doc in documents)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest', type=Path)
    parser.add_argument('--split', choices=['smoke', 'demo', 'tuning', 'final'], required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = prepare(args.manifest, args.split, args.output)
    except (OSError, ValueError, KeyError, TypeError):
        print(json.dumps({'valid': False, 'error': 'capture_dataset_preparation_failed', 'providerCalled': False}))
        return 1
    print(json.dumps({'valid': True, **result}))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
