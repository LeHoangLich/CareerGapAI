"""Check exported CV text, page bounds, and VISIBLE page counters. Requires pdfplumber.

Text presence alone does not prove an absolute footer was drawn on the page.
No document text or contact details are printed.
"""
import argparse
import json
from pathlib import Path
import pdfplumber

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('pdfs', nargs='+', type=Path)
args = parser.parse_args()
results = []
for index, path in enumerate(args.pdfs):
    errors = []
    with pdfplumber.open(path) as pdf:
        for number, page in enumerate(pdf.pages, 1):
            words = page.extract_words()
            footer = sorted([word for word in words if page.height - 42 <= word['top'] <= page.height - 5], key=lambda word: word['x0'])
            if ' '.join(word['text'] for word in footer) != f'{number} / {len(pdf.pages)}':
                errors.append(f'page[{number}]:visible_counter_missing')
            if any(char['x0'] < -0.5 or char['x1'] > page.width + 0.5 or char['top'] < -0.5 or char['bottom'] > page.height + 0.5 for char in page.chars):
                errors.append(f'page[{number}]:text_outside_page')
            text = page.extract_text() or ''
            if not text.strip() or '\ufffd' in text:
                errors.append(f'page[{number}]:text_unreadable')
        results.append({'artifactIndex': index, 'pages': len(pdf.pages), 'errors': errors, 'valid': not errors})
print(json.dumps(results))
raise SystemExit(0 if all(result['valid'] for result in results) else 1)
