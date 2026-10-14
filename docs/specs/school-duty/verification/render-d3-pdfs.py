"""Rasterize generated synthetic D3 PDFs. Never consumes the private source samples."""
import hashlib
import json
import subprocess
from pathlib import Path
import fitz

folder = Path(__file__).parent / "d3-pdf"
results = []
for source in sorted(folder.glob("*.pdf")):
    document = fitz.open(source)
    pages = []
    # Poppler independently renders the embedded Arabic fonts without the MuPDF
    # repeated-text cache artifacts observed on continuation pages in this environment.
    subprocess.run(["pdftoppm", "-r", "115.2", "-png", str(source), str(folder / (source.stem + "-page"))], check=True)
    for index, page in enumerate(document):
        output = folder / f"{source.stem}-page-{index + 1}.png"
        pages.append(output.name)
    results.append({"file": source.name, "sha256": hashlib.sha256(source.read_bytes()).hexdigest().upper(), "pages": pages,
                    "fonts": sorted({font[3] for page in document for font in page.get_fonts()}), "synthetic": True, "rasterizer": "Poppler pdftoppm; PyMuPDF font inventory only"})
(folder / "rendered-pages.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
print(json.dumps(results, indent=2))
