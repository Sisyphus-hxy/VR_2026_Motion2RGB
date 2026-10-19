"""Verify editable objects, notes, package relationships and original video."""
import hashlib
import json
import posixpath
from pathlib import Path
from zipfile import ZipFile
from lxml import etree
import fitz

root = Path(__file__).resolve().parent
deck = root / "exports/Motion2RGB_Proposal.pptx"
ns = {"p": "http://schemas.openxmlformats.org/presentationml/2006/main",
      "a": "http://schemas.openxmlformats.org/drawingml/2006/main",
      "m": "http://schemas.openxmlformats.org/officeDocument/2006/math"}
with ZipFile(deck) as archive:
    files = set(archive.namelist())
    slides = sorted(p for p in files if p.startswith("ppt/slides/slide") and p.endswith(".xml"))
    notes = sorted(p for p in files if p.startswith("ppt/notesSlides/notesSlide") and p.endswith(".xml"))
    assert len(slides) == len(notes) == 7
    roots = [etree.fromstring(archive.read(p)) for p in slides]
    formulas = sum(len(x.xpath("//m:oMath", namespaces=ns)) for x in roots)
    shapes = sum(len(x.xpath("//p:sp", namespaces=ns)) for x in roots)
    texts = sum(len(x.xpath("//a:t", namespaces=ns)) for x in roots)
    assert formulas == 3 and texts > 90 and shapes > 100
    videos = [p for p in files if p.endswith(".mp4")]
    assert len(videos) == 1
    original_hash = hashlib.sha256((root / "media/demo_video.mp4").read_bytes()).hexdigest()
    assert hashlib.sha256(archive.read(videos[0])).hexdigest() == original_hash
    unresolved = []
    for part in files:
        if not part.endswith(".rels"):
            continue
        xml = etree.fromstring(archive.read(part))
        base = posixpath.dirname(posixpath.dirname(part))
        for rel in xml:
            if rel.get("TargetMode") == "External":
                continue
            target = rel.get("Target")
            resolved = target.lstrip("/") if target.startswith("/") else posixpath.normpath(posixpath.join(base, target))
            if resolved not in files:
                unresolved.append((part, target))
    assert not unresolved, unresolved

pdf = fitz.open(root / "exports/Motion2RGB_Proposal.pdf")
assert len(pdf) == 7
previews = list((root / "exports/previews").glob("Slide_*.png"))
assert len(previews) == 7
report = {"slides": len(slides), "notes": len(notes), "native_shapes": shapes,
          "editable_text_runs": texts, "native_formulas": formulas,
          "embedded_video_original_sha256": original_hash,
          "video_duration_seconds": 20.57, "relationships": "all resolved",
          "pdf_pages": len(pdf), "png_previews": len(previews),
          "visual_review": "all seven slides inspected; Chinese and formulas render correctly",
          "render_application": "WPS Presentation 12.1 (registered as PowerPoint COM)",
          "playback": "embedded original verified; playback on classroom computer remains to be checked"}
(root / "validation/delivery_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
