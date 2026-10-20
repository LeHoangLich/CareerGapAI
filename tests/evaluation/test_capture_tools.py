import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]


def module(name, file):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'scripts' / file)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


PREPARE = module('capture_prepare', 'prepare-cv-capture-dataset.py')
ASSEMBLE = module('capture_assemble', 'assemble-cv-captures.py')


class CaptureToolTests(unittest.TestCase):
    def setUp(self):
        private = ROOT / '.local/evaluation'
        private.mkdir(parents=True, exist_ok=True)
        self.folder = Path(tempfile.mkdtemp(prefix='capture-tools-', dir=private))
        self.manifest_path = self.folder / 'corpus.json'
        self.dataset_path = self.folder / 'dataset.json'
        self.capture_path = self.folder / 'export.json'
        self.mapping_path = self.folder / 'mapping.json'
        self.output = self.folder / 'run.json'
        source = self.folder / 'synthetic.docx'
        with ZipFile(source, 'w') as archive:
            archive.writestr('word/document.xml', '<synthetic>React</synthetic>')
        self.sha = hashlib.sha256(source.read_bytes()).hexdigest()
        doc = {'document_id': 'cv-001', 'group_id': 'person-001', 'kind': 'cv', 'language': 'vi', 'format': 'docx',
               'layout': 'paragraphs', 'sha256': self.sha, 'hash_kind': 'file_sha256', 'synthetic': True,
               'private_source_ref': 'synthetic.docx', 'rights_status': 'verified', 'rights_evidence_ref': 'synthetic-owned',
               'allowed_uses': ['local_evaluation'], 'provider_transfer_allowed': False, 'split': 'smoke'}
        self.manifest = {'schema_version': 1, 'corpus_id': 'synthetic-capture-tools', 'documents': [doc],
                         'field_labels': [{'document_id': 'cv-001', 'document_sha256': self.sha, 'field_path': 'skills[0]',
                                           'value': 'React', 'status': 'present', 'provenance': 'document_span',
                                           'locator': {'paragraph': 1, 'quote': 'React'}, 'review_status': 'self_reviewed', 'label_version': 'v1'}]}

    def tearDown(self):
        if self.folder.resolve().parent == (ROOT / '.local/evaluation').resolve() and self.folder.name.startswith('capture-tools-'):
            shutil.rmtree(self.folder)

    def prepare(self):
        self.manifest_path.write_text(json.dumps(self.manifest), encoding='utf-8')
        return PREPARE.prepare(self.manifest_path, 'smoke', self.dataset_path)

    def export(self):
        export = {'schemaVersion': 1, 'contract': 'cv-fields-v3', 'parser': 'synthetic-parser', 'provider': 'Fake',
                  'model': 'synthetic-model', 'method': 'synthetic-method', 'promptSha256': 'a' * 64, 'parameters': {},
                  'beforeUserCorrection': True, 'unit': 'test_credit',
                  'document': {'sourceSha256': self.sha, 'rawOutput': '{"fields":[]}', 'finishReason': 'stop',
                               'errorCode': None, 'inputTokens': 0, 'outputTokens': 0, 'cost': 0, 'elapsedMs': 1}}
        self.capture_path.write_text(json.dumps(export), encoding='utf-8')
        self.mapping_path.write_text(json.dumps([{'documentId': 'cv-001', 'captureFile': 'export.json'}]), encoding='utf-8')
        return export

    def test_prepare_preserves_reviewed_labels_and_does_not_grant_provider_transfer(self):
        result = self.prepare()
        self.assertEqual(result['groups'], 1)
        self.assertFalse(result['providerCalled'])
        self.assertEqual(json.loads(self.dataset_path.read_text())['documents'][0]['labels'][0]['path'], 'skills[0]')

    def test_prepare_rejects_withdrawn_rights_and_stale_source_hash(self):
        for field, value in [('rights_status', 'withdrawn'), ('sha256', 'a' * 64)]:
            with self.subTest(field=field):
                original = copy.deepcopy(self.manifest)
                self.manifest['documents'][0][field] = value
                with self.assertRaises(ValueError):
                    self.prepare()
                self.manifest = original

    def test_prepare_rejects_final_group_leakage(self):
        variant = copy.deepcopy(self.manifest['documents'][0])
        variant.update(document_id='cv-002', split='final')
        self.manifest['documents'].append(variant)
        with self.assertRaises(ValueError):
            self.prepare()

    def test_assembly_binds_exact_dataset_hash_and_original_output(self):
        self.prepare(); self.export()
        result = ASSEMBLE.assemble(self.dataset_path, self.mapping_path, self.output)
        self.assertFalse(result['providerCalled'])
        self.assertEqual(json.loads(self.output.read_text())['datasetSha256'], hashlib.sha256(self.dataset_path.read_bytes()).hexdigest())

    def test_assembly_rejects_changed_source_corrected_output_and_duplicate_mapping(self):
        self.prepare()
        for fault in ('hash', 'corrected', 'duplicate'):
            with self.subTest(fault=fault):
                export = self.export()
                if fault == 'hash': export['document']['sourceSha256'] = 'a' * 64
                if fault == 'corrected': export['beforeUserCorrection'] = False
                self.capture_path.write_text(json.dumps(export), encoding='utf-8')
                if fault == 'duplicate':
                    item = {'documentId': 'cv-001', 'captureFile': 'export.json'}
                    self.mapping_path.write_text(json.dumps([item, item]), encoding='utf-8')
                with self.assertRaises(ValueError):
                    ASSEMBLE.assemble(self.dataset_path, self.mapping_path, self.output)

    def test_input_outside_private_workspace_and_overwriting_output_rejected(self):
        self.prepare(); self.export()
        with self.assertRaises(ValueError):
            ASSEMBLE.assemble(self.dataset_path, self.mapping_path, self.dataset_path)
        with self.assertRaises(ValueError):
            PREPARE.prepare(ROOT / 'README.md', 'smoke', self.folder / 'other.json')

    def test_external_reference_rejected_before_shared_validator_reads_it(self):
        self.manifest['documents'][0]['private_source_ref'] = str(ROOT / 'README.md')
        with self.assertRaisesRegex(ValueError, 'private_bounded_source_required'):
            self.prepare()


if __name__ == '__main__':
    unittest.main()
