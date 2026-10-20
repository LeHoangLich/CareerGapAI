"""Synthetic labelled contract smoke only. Requires reportlab; no real CV/provider."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from xml.sax.saxutils import escape
import hashlib
import json
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.local/evaluation/cv-extraction-fixtures'
OUT.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont('NotoSmoke', str(ROOT / 'frontend/public/fonts/NotoSans-Regular.ttf')))
documents = []
for language in ('vi', 'en'):
    fields = {
        'displayName': 'Ứng Viên Tổng Hợp' if language == 'vi' else 'Synthetic Candidate',
        'email': 'synthetic@example.invalid', 'phone': '+84 900 000 001',
        'location': 'Hồ Chí Minh' if language == 'vi' else 'Test City',
        'headline': 'Kỹ sư phần mềm' if language == 'vi' else 'Software Developer',
        'summary': 'Xây dựng dữ liệu thử.' if language == 'vi' else 'Builds synthetic fixtures.',
        'skills[0]': 'React', 'skills[1]': 'TypeScript', 'skills[2]': 'PostgreSQL',
        'experience[0].role': 'Developer', 'experience[0].organization': 'Synthetic Company',
        'experience[0].period': '2024-2026', 'experience[0].description': 'Built test software.',
        'projects[0].name': 'Test project', 'projects[0].context': 'Synthetic demo', 'projects[0].description': 'Built a fixture.',
        'education[0].program': 'Computer Science', 'education[0].institution': 'Synthetic University',
        'education[0].period': '2020-2024', 'education[0].details': 'Synthetic study.',
        'certifications[0]': 'Synthetic Certificate'
    }
    lines = [f'{key}: {value}' for key, value in fields.items()]
    for form in ('docx', 'pdf'):
        name = f'full-cv-{language}.{form}'
        path = OUT / name
        if form == 'docx':
            paragraphs = ''.join(f'<w:p><w:r><w:t>{escape(line)}</w:t></w:r></w:p>' for line in lines)
            with ZipFile(path, 'w', ZIP_DEFLATED) as archive:
                archive.writestr('[Content_Types].xml', '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
                archive.writestr('_rels/.rels', '<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
                archive.writestr('word/document.xml', '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>' + paragraphs + '</w:body></w:document>')
        else:
            pdf = canvas.Canvas(str(path), pagesize=(842, 595)); pdf.setFont('NotoSmoke', 9)
            for i, line in enumerate(lines):
                column, row = divmod(i, 11)
                pdf.drawString(26 + column * 410, 558 - row * 40, line)
            pdf.save()
        documents.append({'id': name.replace('.', '-'), 'groupId': 'synthetic-full-cv-person-001', 'language': language,
            'format': form, 'layout': 'two_columns' if form == 'pdf' else 'paragraphs', 'file': name,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'expected': [{'path': key, 'value': value} for key, value in fields.items()], 'expectedError': None})
for name, error in [('no-text.pdf', 'no_text_ocr_unsupported'), ('broken.pdf', 'file_unreadable')]:
    path = OUT / name
    if name == 'no-text.pdf':
        pdf = canvas.Canvas(str(path)); pdf.rect(40, 40, 200, 200); pdf.save()
    else:
        path.write_bytes(b'%PDF-broken synthetic fixture')
    documents.append({'id': name.replace('.', '-'), 'groupId': 'synthetic-invalid-' + name.replace('.', '-'), 'language': 'unknown',
        'format': 'pdf', 'layout': 'no_text' if name == 'no-text.pdf' else 'invalid', 'file': name,
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'expected': [], 'expectedError': error})
(OUT / 'dataset.json').write_text(json.dumps({'datasetId': 'full-cv-contract-smoke-v1', 'synthetic': True, 'split': 'smoke', 'documents': documents}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Generated 6 synthetic files with pre-correction field labels. Four vi/en/format variants share one original group; not quality evidence.')
