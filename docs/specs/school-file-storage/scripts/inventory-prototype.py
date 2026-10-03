"""Read prototype sources as data; never import or run their code.

Outputs only aggregate data, public template keys, hashes and source locations.
No teacher names, original file names/paths, credentials or file bytes are exported.
"""
import argparse
import csv
import hashlib
import io
import json
import re
from collections import Counter
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path


class Attributes(HTMLParser):
    def __init__(self):
        super().__init__()
        self.ids = set()
        self.actions = Counter()
        self.lines = {}

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if values.get('id'):
            self.ids.add(values['id'])
            self.lines[values['id']] = self.getpos()[0]
        for key, value in attrs:
            if key.startswith('on') and value:
                for name in re.findall(r'\b([A-Za-z_]\w*)\s*\(', value):
                    self.actions[name] += 1


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def extract_array(source, variable):
    match = re.search(r'(?:const|let)\s+' + re.escape(variable) + r'\s*=\s*(\[.*\]);\s*$', source, re.M)
    if not match:
        raise ValueError(f'Missing JSON array: {variable}')
    return json.loads(match.group(1)), source.count('\n', 0, match.start()) + 1


def stats(rows, path_key, root):
    keys = sorted({key for row in rows for key in row})
    paths = Counter(str(row.get(path_key, '')).replace('\\', '/') for row in rows)
    complete_rows = Counter(json.dumps(row, sort_keys=True, ensure_ascii=False) for row in rows)
    existing = unsafe = 0
    for row in rows:
        rel = str(row.get(path_key, '')).replace('\\', '/')
        # Only resolve strictly relative paths inside the supplied source package.
        if not rel or re.match(r'^[A-Za-z]:', rel) or rel.startswith('/'):
            unsafe += 1
            continue
        target = (root / rel).resolve()
        if not target.is_relative_to(root.resolve()):
            unsafe += 1
        elif target.is_file():
            existing += 1
    return {
        'records': len(rows), 'keys': keys,
        'missingOrEmptyByField': {key: sum(row.get(key) in (None, '') for row in rows) for key in keys},
        'distinctReferencePaths': len(paths),
        'duplicatePathOccurrences': sum(v - 1 for v in paths.values()),
        'exactDuplicateRows': sum(v - 1 for v in complete_rows.values()),
        'existingOriginalBytesInPackage': existing,
        'nonRelativeOrUnsafeReferencePaths': unsafe,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True, help='Directory containing index.html and app_server.py')
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parent.parent / 'baseline')
    args = parser.parse_args()
    source = args.source.resolve()
    base = source.parent if source.name == '[DEVELOPER_SYSTEM_CORE - DO_NOT_MODIFY]' else source
    files = sorted(p for p in source.iterdir() if p.is_file() and p.suffix.lower() in ('.py', '.html', '.json', '.csv', '.md'))
    originals = {p.name: p.read_bytes() for p in files}
    texts = {name: data.decode('utf-8-sig') for name, data in originals.items()}
    index = texts['index.html']
    attrs = Attributes()
    attrs.feed(index)
    tracker, tracker_line = extract_array(index, 'rawItemsData')
    explorer, explorer_line = extract_array(index, 'explorerDB')
    explorer_files = [file for domain in explorer for folder in domain['subfolders'] for file in folder['files']]
    embedded_matrix, matrix_line = extract_array(index, 'matrix192Data')
    catalog = json.loads(texts['فهرس_الملفات_والشواهد_الشامل.json'])
    matrix = json.loads(texts['مصفوفة_استيفاء_النواقص.json'])
    csv_matrix = list(csv.DictReader(io.StringIO(texts['مصفوفة_استيفاء_النواقص.csv'])))
    functions = [
        {'function': m.group(1), 'line': index.count('\n', 0, m.start()) + 1,
         'inlineHandlerOccurrences': attrs.actions[m.group(1)]}
        for m in re.finditer(r'^\s*(?:async\s+)?function\s+(\w+)\s*\(', index, re.M)
    ]
    inventory = []
    for p in files:
        text = texts[p.name]
        record = {
            'resource': p.name, 'bytes': len(originals[p.name]),
            'lastModifiedUtc': datetime.fromtimestamp(p.stat().st_mtime, timezone.utc).isoformat(),
            'sha256': digest(originals[p.name]), 'lines': len(text.splitlines()),
        }
        if p.suffix == '.json':
            value = json.loads(text)
            record['schema'] = sorted(value.keys()) if isinstance(value, dict) else sorted({k for row in value for k in row})
            record['records'] = len(value['files']) if isinstance(value, dict) and 'files' in value else (len(value) if isinstance(value, list) else None)
        if p.suffix == '.csv':
            record['schema'] = list(csv_matrix[0])
            record['records'] = len(csv_matrix)
        inventory.append(record)
    count_locations = {}
    for literal in ('4116', '4617', '4994', '4,116', '4,617', '4,994', '14 محطة', 'totalSlides = 13'):
        count_locations[literal] = {
            name: [i for i, line in enumerate(text.splitlines(), 1) if literal in line]
            for name, text in texts.items() if literal in text
        }
    path_categories_disagree = 0
    for row in matrix:
        if str(row['category']) not in str(row['rel_path']).replace('\\', '/'):
            path_categories_disagree += 1
    probe_ids = ['containerMatrix192', 'matrixTableBody', 'matrixSearchInput',
                 'matrixRoleFilter', 'matrixCategoryFilter', 'trackerDynamicContainer',
                 'uploadHelperModal', 'explorerFileGrid', 'slideNavList']
    standalone_name = 'مصفوفة_النماذج_الفارغة_واستيفاء_النواقص.html'
    standalone = Attributes()
    standalone.feed(texts[standalone_name])
    report = {
        'capturedAtUtc': datetime.now(timezone.utc).isoformat(),
        'sourceDirectory': str(source), 'prototypeBaseDirectory': str(base),
        'method': 'Static parsing and file existence checks; prototype code never executed',
        'sources': inventory,
        'slideIds': sorted((x for x in attrs.ids if re.fullmatch(r'slide-\d+', x)), key=lambda x: int(x.split('-')[1])),
        'domProbes': {x: {'present': x in attrs.ids, 'line': attrs.lines.get(x)} for x in probe_ids},
        'indexCatalog': {**stats(catalog['files'], 'relPath', base),
                         'declaredTotal': catalog['totalFiles'],
                         'mainDomains': dict(sorted(Counter(row['mainDomain'] for row in catalog['files']).items())),
                         'standards': dict(sorted(Counter(row['standard'] for row in catalog['files']).items())),
                         'extensions': dict(sorted(Counter(row['ext'].lower() for row in catalog['files']).items()))},
        'embeddedExplorer': {**stats(explorer_files, 'relPath', base), 'line': explorer_line,
                             'standardNodes': len(explorer), 'declaredTotal': sum(row['totalFiles'] for row in explorer)},
        'matrixJson': {**stats(matrix, 'rel_path', base),
                       'categories': dict(sorted(Counter(row['category'] for row in matrix).items())),
                       'roles': dict(sorted(Counter(row['role'] for row in matrix).items())),
                       'statuses': dict(Counter(row['status'] for row in matrix)),
                       'importance': dict(Counter(row['importance'] for row in matrix)),
                       'categoryNotInReferencePath': path_categories_disagree,
                       'rows': [{'ordinal': i, 'sourceRowSha256': digest(json.dumps(row, sort_keys=True, ensure_ascii=False).encode()),
                                 'category': row['category'], 'role': row['role'], 'importance': row['importance'],
                                 'status': row['status'], 'availability': 'ReferenceOnly'} for i, row in enumerate(matrix, 1)]},
        'matrixCsvRecords': len(csv_matrix),
        'embeddedMatrix': {**stats(embedded_matrix, 'rel_path', base), 'line': matrix_line,
                           'identicalToJson': embedded_matrix == matrix},
        'tracker': {
            'records': len(tracker), 'line': tracker_line,
            'schema': sorted({key for row in tracker for key in row}),
            'duplicateIds': len(tracker) - len({row['id'] for row in tracker}),
            'missingOrEmptyByField': {key: sum(row.get(key) in (None, '') for row in tracker) for key in sorted({k for row in tracker for k in row})},
            'domainKeys': dict(sorted(Counter(row['domainKey'] for row in tracker).items())),
            'statuses': dict(Counter(row['status'] for row in tracker)),
            'items': [{'sourceId': row['id'], 'ordinal': row['num'], 'domainKey': row['domainKey'],
                       'sourceRowSha256': digest(json.dumps(row, sort_keys=True, ensure_ascii=False).encode()),
                       'status': row['status'], 'targetPathPresent': bool(row.get('targetRelPath')),
                       'mappingStatus': 'Needs human requirement and responsible-user mapping'} for row in tracker],
        },
        'standaloneMatrix': {'resource': standalone_name, 'domIds': sorted(standalone.ids),
                             'functions': [{'function': m.group(1), 'line': texts[standalone_name].count('\n', 0, m.start()) + 1}
                                           for m in re.finditer(r'^\s*(?:async\s+)?function\s+(\w+)\s*\(', texts[standalone_name], re.M)]},
        'indexFunctions': functions, 'literalLocations': count_locations,
        'privacy': 'No file names/reference paths, personal teacher/responsible names or original file bytes exported',
    }
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'prototype-inventory.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    lines = ['# S0 — سجل المصادر المرجعية', '', 'Generated by `scripts/inventory-prototype.py`; see `prototype-inventory.json` for schemas and aggregate findings.', '',
             '| Resource | Bytes | Last modified UTC | SHA-256 |', '|---|---|---|---|']
    for row in inventory:
        lines.append(f"| {row['resource']} | {row['bytes']} | {row['lastModifiedUtc']} | `{row['sha256']}` |")
    (args.output / 'source-manifest.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    # Verify that collection changed none of the source resources.
    if any(p.read_bytes() != originals[p.name] for p in files):
        raise RuntimeError('A prototype source changed during collection; snapshot is not stable.')
    print(json.dumps({'resources': len(files), 'slides': len(report['slideIds']), 'functions': len(functions),
                      'catalogRecords': len(catalog['files']), 'matrixRecords': len(matrix), 'trackerItems': len(tracker),
                      'availableOriginalBytes': report['indexCatalog']['existingOriginalBytesInPackage'],
                      'domProbes': report['domProbes'], 'categoryPathConflicts': path_categories_disagree,
                      'embeddedMatrixIdentical': report['embeddedMatrix']['identicalToJson']}, ensure_ascii=True))


if __name__ == '__main__':
    main()
