"""Read-only, repeatable source inventory; no SQL, application or provider calls."""
from pathlib import Path
import hashlib
import json
import re
import argparse

ROOT = Path(__file__).resolve().parents[4]
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'verification' / 'source-inventory.json')
parser.add_argument('--scope', default='C32 design discovery; lexical matches, not a proof of semantic coverage')
args = parser.parse_args()
OUTPUT = args.output.resolve()
PATTERN = re.compile(r'\b(?:Classroom(?:Id|Ids|Name|Label|s)?|ClassLabel|GradeClass|InstructorClass(?:es)?|GetAssignedClassroomsAsync|IsClassroomAssignedAsync)\b', re.I)
records = []
for directory in [ROOT / 'backend', ROOT / 'frontend/src/app']:
    for path in sorted(directory.rglob('*')):
        if path.suffix not in {'.cs', '.ts', '.html'} or any(part in {'obj', 'bin', 'node_modules'} for part in path.parts):
            continue
        text = path.read_text(encoding='utf-8-sig', errors='replace')
        lines = [i for i, line in enumerate(text.splitlines(), 1) if PATTERN.search(line)]
        if not lines:
            continue
        relative = path.relative_to(ROOT).as_posix()
        category = ('migration' if '/Migrations/' in relative else 'test' if '/AlFalah.Tests/' in relative or '.spec.' in relative
                    else 'fixture' if '/fixtures/' in relative.lower() or '/Seeders/' in relative else 'runtime')
        records.append({'path': relative, 'category': category, 'lines': lines,
                        'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest()})
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
OUTPUT.write_text(json.dumps({'scope': args.scope,
                             'pattern': PATTERN.pattern,
                             'counts': {kind: sum(r['category'] == kind for r in records) for kind in ['runtime', 'test', 'fixture', 'migration']},
                             'files': records}, indent=2), encoding='utf-8')
print(json.dumps({'output': str(OUTPUT.relative_to(ROOT)), 'matchedFiles': len(records)}))
