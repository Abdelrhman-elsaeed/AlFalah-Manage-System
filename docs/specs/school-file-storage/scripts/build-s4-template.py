"""Curate the S4 template as data only. Never executes prototype source code."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
BASELINE = ROOT / 'docs/specs/school-file-storage/baseline/prototype-inventory.json'
baseline = json.loads(BASELINE.read_text(encoding='utf-8'))
source = Path(baseline['sourceDirectory'])
raw = (source / 'index.html').read_bytes()
assert hashlib.sha256(raw).hexdigest().upper() == next(x['sha256'] for x in baseline['sources'] if x['resource'] == 'index.html')
text = raw.decode('utf-8-sig')
items = json.loads(re.search(r'(?:const|let)\s+rawItemsData\s*=\s*(\[.*\]);\s*$', text, re.M)[1])
people = {x[k] for x in items for k in ('responsible', 'personGroup') if x.get(k)}

def reference(value):
    value = value.replace('\\', '/')
    for name in sorted(people, key=len, reverse=True):
        value = value.replace(name, '{responsible}')
    # Physical Windows/absolute paths never become product navigation.
    if re.match(r'^[A-Za-z]:', value) or value.startswith('/'):
        value = 'مرجع المصدر/' + value.rsplit('/', 1)[-1]
    return value

def fingerprint(row):
    return hashlib.sha256(json.dumps(row, sort_keys=True, ensure_ascii=False).encode()).hexdigest().upper()

standards = [('1.1', 'التخطيط'), ('1.2', 'قيادة العملية التعليمية'), ('1.3', 'المجتمع المدرسي'),
             ('1.4', 'التطوير المؤسسي'), ('1.5', 'حقوق المتعلم وحمايته'), ('2.1', 'بناء خبرات التعلم'),
             ('2.2', 'تقويم التعلم'), ('3.1', 'التحصيل التعليمي'), ('3.2', 'التطور الشخصي والصحي'),
             ('4.1', 'المبنى المدرسي'), ('4.2', 'الأمن والسلامة')]
task_candidates = {'1.1': [], '1.2': ['RP-01', 'RP-02'], '1.3': ['SP-03', 'PC-03'],
                   '1.4': ['CV-05', 'CV-06', 'CV-07', 'PC-02'], '1.5': [],
                   '2.1': ['EN-01', 'EN-05', 'CP-01', 'CP-02', 'CP-03'],
                   '2.2': ['AS-01', 'AS-02', 'AS-03', 'PC-01'], '3.1': ['AS-03', 'RP-01', 'RP-02', 'RP-03'],
                   '3.2': ['SP-01', 'SP-02'], '4.1': [], '4.2': []}
result = {
    'version': 1, 'name': 'قالب التقويم الذاتي المرجعي — الإصدار الأول',
    'rounding': 'DecimalTwoPlacesAwayFromZero', 'sourceName': 'index.html/rawItemsData',
    'sourceSha256': hashlib.sha256(raw).hexdigest().upper(),
    'domains': [{'code': str(i + 1), 'name': n, 'sortOrder': i + 1} for i, n in enumerate(
        ['الإدارة المدرسية', 'التعليم والتعلم', 'نواتج التعلم', 'البيئة المدرسية'])],
    'standards': [{'code': c, 'name': n, 'sortOrder': i + 1} for i, (c, n) in enumerate(standards)],
    'items': [], 'matrixReferences': []
}
for row in items:
    code = row['domainKey'].split()[0]
    result['items'].append({
        'sourceKey': row['id'], 'sortOrder': row['num'], 'standardCode': code,
        'displayName': row['doc'], 'responsibleRole': row['personRole'],
        'importance': 'Critical' if code == '4.2' else 'Normal', 'importanceReason': row['importance'],
        'referencePath': reference(row['targetRelPath']),
        'completionAction': 'تجهيز شاهد للإجراء: ' + row['doc'] + '، ثم ربطه وإرساله للمراجعة المستقلة.',
        'sourceSha256': fingerprint(row), 'candidateTaskCodes': task_candidates[code],
        'mappingStatus': 'NeedsResponsibleAndContentReview', 'inheritedStatusApplied': False
    })
matrix_raw = (source / 'مصفوفة_استيفاء_النواقص.json').read_bytes()
assert hashlib.sha256(matrix_raw).hexdigest().upper() == next(x['sha256'] for x in baseline['sources'] if x['resource'] == 'مصفوفة_استيفاء_النواقص.json')
for i, row in enumerate(json.loads(matrix_raw.decode('utf-8-sig'))):
    result['matrixReferences'].append({'ordinal': i + 1, 'sourceSha256': fingerprint(row),
                                       'sourceResourceSha256': hashlib.sha256(matrix_raw).hexdigest().upper(),
                                       'state': 'ReferenceOnly'})
assert len(result['items']) == 36 and len(result['matrixReferences']) == 145
target = ROOT / 'backend/AlFalah.Application/Storage/Templates/self-evaluation-v1.json'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Curated 4 domains / 11 standards / 36 items / 145 reference fingerprints; no people, bytes or approvals.')
