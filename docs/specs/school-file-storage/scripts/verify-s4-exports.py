"""Inspect actual isolated-test CSV/XLSX/PDF bytes, never the running API/Drive."""
import csv
import hashlib
import io
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

import fitz

ROOT = Path(__file__).resolve().parents[4]
folder = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'frontend/test-results/storage/s4-exports'
expected = json.loads((folder / 'expected.json').read_text(encoding='utf-8'))
summary = expected['summary']
metrics = [summary['overall'], *summary['domains'], *summary['standards']]
rows = expected['rows']
raw = (folder / 'filtered.csv').read_bytes()
assert raw.startswith(b'\xef\xbb\xbf')
csv_rows = list(csv.reader(io.StringIO(raw.decode('utf-8-sig'))))
assert csv_rows[0] == ['المدرسة', summary['schoolName']]
assert json.loads(next(r[1] for r in csv_rows if r[0] == 'المرشحات')) == summary['filters']
for metric in metrics:
    line = next(r for r in csv_rows if len(r) == 12 and r[0] == metric['code'])
    assert line[3:6] == [str(metric['numerator']), str(metric['denominator']),
                            '' if metric['percentage'] is None else f"{metric['percentage']:.2f}"]
csv_requirements = [r for r in csv_rows if r[0] in {x['code'] for x in rows}]
assert len(csv_requirements) == len(rows)
assert [r[1] for r in csv_requirements] == [r['name'] for r in rows]

NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(folder / 'filtered.xlsx') as archive:
    strings_root = ET.fromstring(archive.read('xl/sharedStrings.xml'))
    strings = [''.join(s.itertext()) for s in strings_root.findall('s:si', NS)]
    def sheet(number):
        doc = ET.fromstring(archive.read(f'xl/worksheets/sheet{number}.xml'))
        assert doc.find('s:sheetViews/s:sheetView', NS).get('rightToLeft') == '1'
        assert not doc.findall('.//s:f', NS)
        result = []
        for row in doc.findall('s:sheetData/s:row', NS):
            cells = []
            for cell in row.findall('s:c', NS):
                val = cell.find('s:v', NS)
                letters = re.match(r'[A-Z]+', cell.get('r')).group()
                column = 0
                for char in letters:
                    column = column * 26 + ord(char) - 64
                while len(cells) < column:
                    cells.append('')
                cells[column - 1] = strings[int(val.text)] if cell.get('t') == 's' else (val.text if val is not None else '')
            result.append(cells)
        return result
    excel_metrics = sheet(2)
    for actual, metric in zip(excel_metrics[1:], metrics):
        assert actual[3:6] == [str(metric['numerator']), str(metric['denominator']),
                                '' if metric['percentage'] is None else f"{metric['percentage']:.2f}"]
    excel_requirements = sheet(3)
    assert [r[1] for r in excel_requirements[1:]] == [r['name'] for r in rows]
    assert len(sheet(4)) == len(expected['manual']) + 1
    index = sheet(5)

pdfs = []
for filename, ratio, percentage in [('filtered.pdf', '1/4', '25.00'), ('full.pdf', '1/36', '2.78'), ('empty.pdf', '0/0', None)]:
    doc = fitz.open(folder / filename)
    text = ''.join(page.get_text() for page in doc)
    assert ratio in text
    if filename == 'filtered.pdf':
        assert re.findall(r'\d+/\d+', doc[0].get_text())[:16] == [f"{m['numerator']}/{m['denominator']}" for m in metrics]
        percentages = [a or b for a, b in re.findall(r'(\d+\.\d+)%|%(\d+\.\d+)', doc[0].get_text())]
        assert percentages == [f"{m['percentage']:.2f}" for m in metrics if m['percentage'] is not None]
    if percentage is not None:
        assert percentage in text
    else:
        assert '100%' not in text and '100.00%' not in text
    fonts = set()
    char_count = 0
    for page in doc:
        fonts.update(font[3] for font in page.get_fonts())
        for block in page.get_text('rawdict')['blocks']:
            for line in block.get('lines', []):
                for span in line['spans']:
                    for char in span['chars']:
                        x0, y0, x1, y1 = char['bbox']
                        assert x0 >= 20 and y0 >= 20 and x1 <= page.rect.width - 20 and y1 <= page.rect.height - 18, (filename, page.number, char)
                        char_count += 1
        assert any('Amiri' in font[3] and len(doc.extract_font(font[0])[-1]) > 0 for font in page.get_fonts())
    for number in {0, len(doc) - 1}:
        doc[number].get_pixmap(matrix=fitz.Matrix(1.3, 1.3)).save(folder / f'{Path(filename).stem}-{number+1}.png')
    # The existing QuestPDF 2024.3.4 emits zero ToUnicode entries for shaped Amiri
    # glyphs. Rendering is correct; Arabic search/copy is a separately recorded limit.
    zero_maps = sum(len(re.findall(rb'<[0-9A-Fa-f]{4}> <0000>', doc.xref_stream(ref) or b''))
                    for ref in range(1, doc.xref_length()) if doc.xref_is_stream(ref))
    pdfs.append({'file': filename, 'pages': len(doc), 'charactersWithinPage': char_count,
                 'fonts': sorted(fonts), 'ratio': ratio, 'percentage': percentage, 'metricsCompared': 16 if filename == 'filtered.pdf' else 1,
                 'zeroShapedGlyphUnicodeMappings': zero_maps,
                 'arabicSearchCopySupported': zero_maps == 0})

report = {'stage': 'S4', 'source': 'actual generated bytes from isolated SQL + fake Drive',
          'csv': {'bom': True, 'metricsCompared': len(metrics), 'requirementsCompared': len(rows), 'filtersEqual': True},
          'excel': {'rtlSheets': 5, 'metricsCompared': len(metrics), 'requirementsCompared': len(rows),
                    'manualJudgmentsCompared': len(expected['manual']), 'indexRows': len(index)-1, 'formulaCells': 0},
          'pdf': pdfs, 'visualReview': 'See S4 report; Arabic shaping/RTL, pagination, mixed codes and retained judgments reviewed on rendered PNGs.',
          'files': {f.name: {'bytes': f.stat().st_size, 'sha256': hashlib.sha256(f.read_bytes()).hexdigest()}
                    for f in folder.iterdir() if f.suffix in {'.csv', '.xlsx', '.pdf'}},
          'liveGoogleVerified': False}
path = ROOT / 'docs/specs/school-file-storage/verification/s4-export-comparison.json'
path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'metrics': len(metrics), 'rows': len(rows), 'pdfPages': [p['pages'] for p in pdfs]}, ensure_ascii=False))
