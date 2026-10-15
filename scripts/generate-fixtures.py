"""Synthetic test data only; output .local/fixtures. Optional tool, not a backend service.
Requires reportlab. Uses the checked-in OFL font; no network or personal information.
"""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from xml.sax.saxutils import escape
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

root = Path(__file__).resolve().parents[1]
out = root / '.local' / 'fixtures'
out.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont('Noto', str(root / 'frontend/public/fonts/NotoSans-Regular.ttf')))
texts = {
    'vi': ['DỮ LIỆU GIẢ - Hồ sơ thử nghiệm', 'Kỹ năng: React, TypeScript, PostgreSQL.', 'Dự án: Xây dựng ứng dụng quản lý công việc.', 'Kinh nghiệm: Docker và kiểm thử dữ liệu.'],
    'en': ['SYNTHETIC DATA - test resume', 'Skills: React, TypeScript, PostgreSQL.', 'Project: task management application.', 'Experience: Docker and software testing.'],
}
for language, lines in texts.items():
    xml = ''.join(f'<w:p><w:r><w:t>{escape(line)}</w:t></w:r></w:p>' for line in lines[:2])
    xml += '<w:tbl><w:tr>' + ''.join(f'<w:tc><w:p><w:r><w:t>{escape(line)}</w:t></w:r></w:p></w:tc>' for line in lines[2:]) + '</w:tr></w:tbl>'
    with ZipFile(out / f'synthetic-{language}.docx', 'w', ZIP_DEFLATED) as z:
        z.writestr('[Content_Types].xml', '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
        z.writestr('_rels/.rels', '<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
        z.writestr('word/document.xml', '<?xml version="1.0"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>' + xml + '</w:body></w:document>')
    c = canvas.Canvas(str(out / f'synthetic-{language}-columns.pdf'), pagesize=(595, 842))
    c.setFont('Noto', 15)
    c.drawString(40, 780, lines[0])
    c.setFont('Noto', 10)
    c.drawString(40, 720, lines[1])
    for i, line in enumerate(lines[2:]):
        c.drawString(320, 720 - i * 70, line[:32])
        c.drawString(320, 704 - i * 70, line[32:])
    c.save()
c = canvas.Canvas(str(out / 'synthetic-no-text.pdf')); c.rect(50, 500, 400, 200); c.save()
print('Generated 5 synthetic fixtures; no real CV or personal data.')
