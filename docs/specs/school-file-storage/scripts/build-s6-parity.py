"""Join all S0 source rows to retained stage evidence; acceptance is never inferred."""
import json
import re
from pathlib import Path

spec = Path(__file__).resolve().parents[1]
source = spec / 'baseline/prototype-parity.md'
s4 = {row['id']: row for row in json.loads((spec / 'verification/s4-parity.json').read_text(encoding='utf-8'))['rows']}
rows = []
for line in source.read_text(encoding='utf-8').splitlines():
    cells = [cell.strip() for cell in line.strip().strip('|').split('|')]
    if not cells or not re.fullmatch(r'(?:P-\d{3}|[MD]-\d{2})', cells[0]):
        continue
    identifier = cells[0]
    if identifier in {row['id'] for row in rows}:
        continue
    main = identifier.startswith('P-')
    screen, owner, scenario = (cells[6], cells[7], cells[8]) if main else (cells[4], cells[4], cells[5])
    stage = re.search(r'S[2346]', owner).group() if re.search(r'S[2346]', owner) else 'S2'
    retained = s4.get(identifier)
    route = retained['route'] if retained else ('/school-manager/storage/imports' if stage == 'S6' else '/school-manager/storage and /instructor/my-files')
    contracts = retained['contracts'] if retained else ('/api/v1/storage/imports preview/rows/review/commit/bytes/reconcile/exceptions.csv' if stage == 'S6' else '/api/v1/storage files/folders/content; S3 requirements/links/reviews/changes')
    evidence = retained['evidence'] if retained else ('PrototypeImportTests + import.spec.ts' if stage == 'S6' else 'StorageLibraryTests, StorageLibrarySqlServerTests, EvidenceWorkflowSqlTests and library/evidence browser suites; stage-level regression evidence')
    difference = 'Authenticated internal preview/download/folder navigation replaces Windows open/server paths; shared platform navigation and SQL state replace prototype handlers. Source counts are reference metadata.'
    if retained:
        difference = 'Retained S4 replacement and limitations; see S4 parity narrative and S6 verification.'
    if stage == 'S6':
        difference = 'Inert JSON/CSV preview/review/commit replaces prototype executable scan; original bytes are a separate verified upload.'
    direct = 'StageEvidence'
    if identifier in ('P-003', 'P-004', 'P-005'):
        difference = 'Prototype theme preference/toggle is not reproduced by the storage feature; current platform styling remains. Owner acceptance of this difference is pending.'
        direct = 'DocumentedGap'
    rows.append(dict(id=identifier, sourceFunction=cells[1], targetAction=cells[2], screen=screen, stage=stage,
                     route=route, contracts=contracts, baselineAcceptanceScenario=scenario, evidence=evidence,
                     verificationScope=direct, difference=difference, ownerAccepted=False))
assert len(rows) == 108 and len({row['id'] for row in rows}) == 108
result = dict(stage='S6', sourceCoverageTotal=108, javascriptRows=92, matrixRows=4, declarativeRows=12,
              meaning='All source rows mapped; helper functions share journeys. This is not 108 independently executed function tests or owner acceptance.',
              operationalCutover=False, ownerParityAccepted=False, rows=rows,
              s5AdditionalEvidence='s5-parity.json and final VisitArchiveSqlTests/PDF pixel comparison; visit archives remain excluded from general library/evidence')
(spec / 'verification/s6-parity.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('108 source rows mapped; owner acceptance remains pending.')
