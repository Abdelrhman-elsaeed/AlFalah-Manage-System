"""Extract the 36 tracker rows as inert JSON, checking the S0 source fingerprint."""
import argparse
import hashlib
import json
import re
from pathlib import Path
parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[4]
inventory = json.loads((root / 'docs/specs/school-file-storage/baseline/prototype-inventory.json').read_text(encoding='utf-8'))
source = Path(inventory['sourceDirectory'])
original = (source / 'index.html').read_bytes()
expected = next(x['sha256'] for x in inventory['sources'] if x['resource'] == 'index.html')
if hashlib.sha256(original).hexdigest().upper() != expected:
    raise SystemExit('Source fingerprint changed; review and inventory the new source first.')
match = re.search(r'(?:const|let)\s+rawItemsData\s*=\s*(\[.*\]);\s*$', original.decode('utf-8-sig'), re.M)
rows = json.loads(match[1]) if match else []
if len(rows) != 36:
    raise SystemExit('Expected exactly 36 tracker rows.')
output = Path(args.output)
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
print('36 inert JSON rows extracted; no JavaScript executed. Output contains source provenance; keep it local.')
