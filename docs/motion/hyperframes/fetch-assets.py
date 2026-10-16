"""Fetch pinned, local rendering dependencies. No network is used during render."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from urllib.request import urlopen

ROOT = Path(__file__).parent / 'assets'
ASSETS = {'gsap.min.js':'https://cdn.jsdelivr.net/npm/gsap@3.14.2/dist/gsap.min.js'}
for weight in ['Regular','Medium','Bold','ExtraBold']:
    ASSETS[f'Tajawal-{weight}.ttf'] = f'https://raw.githubusercontent.com/google/fonts/main/ofl/tajawal/Tajawal-{weight}.ttf'

def fetch(item):
    name,url = item
    target = ROOT / name
    if not target.exists():
        target.write_bytes(urlopen(url,timeout=45).read())
    print(name, target.stat().st_size)

with ThreadPoolExecutor(max_workers=4) as pool:
    list(pool.map(fetch,ASSETS.items()))
