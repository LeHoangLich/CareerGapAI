"""Build an offline replay example from EXISTING synthetic smoke raw outputs.

Run evaluate-cv-extraction.ps1 first. No new CV/PDF, key, provider call or quality claim.
--with-faults explicitly injects controlled missing-field/truncation/JSON errors.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=ROOT / '.local/evaluation/cv-extraction-smoke.json')
    parser.add_argument('--fixtures', type=Path, default=ROOT / '.local/evaluation/cv-extraction-fixtures/dataset.json')
    parser.add_argument('--output-directory', type=Path, required=True)
    parser.add_argument('--with-faults', action='store_true')
    args = parser.parse_args()
    private = (ROOT / '.local').resolve()
    folder = args.output_directory.resolve()
    if not all(path.resolve().is_relative_to(private) for path in (args.source, args.fixtures, folder)) or folder.exists():
        parser.error('Private inputs and a new private output directory required')
    source = json.loads(args.source.read_text(encoding='utf-8-sig'))
    fixtures = json.loads(args.fixtures.read_text(encoding='utf-8-sig'))
    if not fixtures['synthetic'] or source['provider'] != 'Fake':
        parser.error('Synthetic Fake smoke required')
    folder.mkdir(parents=True)
    docs, captures = [], []
    raw_by_id = {doc['id']: doc for doc in source['documents']}
    for index, fixture in enumerate(fixtures['documents']):
        path = (args.fixtures.parent / fixture['file']).resolve()
        if not path.is_relative_to(private) or hashlib.sha256(path.read_bytes()).hexdigest() != fixture['sha256']:
            parser.error('Synthetic source hash mismatch')
        labels = [{'path': field['path'], 'value': field['value'], 'status': 'present', 'quote': field['value'], 'reviewStatus': 'self_reviewed'} for field in fixture['expected']]
        docs.append({key: fixture[key] for key in ('id', 'groupId', 'language', 'format', 'layout', 'sha256')} |
                    {'file': os.path.relpath(path, folder), 'synthetic': True, 'localEvaluationAllowed': True,
                     'rightsEvidenceRef': 'synthetic-generated-by-project', 'labels': labels})
        record = raw_by_id[fixture['id']]
        raw = None
        if not record.get('error'):
            raw_path = (args.source.parent / record['rawOutput']).resolve()
            if not raw_path.is_relative_to(private): parser.error('Private raw output required')
            raw = json.dumps(json.loads(raw_path.read_text(encoding='utf-8-sig'))['extracted'], ensure_ascii=False)
        finish = 'stop' if raw is not None else None
        if args.with_faults and raw is not None:
            if index == 1:
                changed = json.loads(raw); changed['fields'].pop(); raw = json.dumps(changed, ensure_ascii=False)
            if index == 2: finish = 'length'
            if index == 3: raw = '{synthetic-malformed'
        captures.append({'id': fixture['id'], 'sourceSha256': fixture['sha256'], 'rawOutput': raw,
                         'finishReason': finish, 'errorCode': record.get('error'), 'inputTokens': 0, 'outputTokens': 0,
                         'cost': 0, 'elapsedMs': record['elapsedMs']})
    dataset = {'schemaVersion': 1, 'datasetId': 'synthetic-capture-smoke', 'split': 'smoke', 'splitLockedAtUtc': None, 'documents': docs}
    dataset_bytes = (json.dumps(dataset, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    (folder / 'dataset.json').write_bytes(dataset_bytes)
    run = {'schemaVersion': 1, 'datasetSha256': hashlib.sha256(dataset_bytes).hexdigest(), 'contract': 'cv-fields-v3',
           'parser': source['parser'], 'provider': 'Fake', 'model': 'fake-explicit-labels-v1',
           'method': source['method'] + (':controlled-faults' if args.with_faults else ''),
           'promptSha256': source['promptSha256'],
           'parameters': {'controlledFaults': args.with_faults},
           'beforeUserCorrection': True, 'unit': 'test_credit', 'documents': captures}
    (folder / 'captures.json').write_text(json.dumps(run, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'documents': len(docs), 'controlledFaults': args.with_faults, 'providerCalled': False, 'qualityEvidence': False}))


if __name__ == '__main__':
    main()
