"""Package final outputs separately from Unity Assets and build intermediates."""
from pathlib import Path
import shutil
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parent
destination = root.parent / "Exports"
destination.mkdir(exist_ok=True)
for filename in ("Motion2RGB_Proposal.pptx", "Motion2RGB_Proposal.pdf"):
    shutil.copy2(root / "exports" / filename, destination / filename)
shutil.copytree(root / "exports/previews", destination / "Previews", dirs_exist_ok=True)
shutil.copy2(root / "media/demo_video.mp4", destination / "Motion2RGB_Demo.mp4")
notes = (root / "Speaker_Notes.md").read_text(encoding="utf-8")
notes += "\n\n" + (root / "notes/total.md").read_text(encoding="utf-8")
(destination / "Motion2RGB_Speaker_Notes.md").write_text(notes, encoding="utf-8")

sources = ["build_slides.mjs", "design_spec.md", "spec_lock.md", "design_decisions.md",
           "prepare_media.py", "run_skill_windows.py", "embed_demo.py", "render_deck.ps1",
           "reproduce.ps1", "verify_delivery.py", "pack_delivery.py", "BUILD_README.md",
           "Speaker_Notes.md"]
folders = ["svg_output", "svg_final", "images", "media", "notes", "sources", "analysis", "validation"]
with ZipFile(destination / "Motion2RGB_Source.zip", "w", ZIP_DEFLATED, compresslevel=6) as archive:
    for filename in sources:
        archive.write(root / filename, "Motion2RGB_Source/" + filename)
    for folder in folders:
        for file in sorted((root / folder).rglob("*")):
            if file.is_file():
                archive.write(file, "Motion2RGB_Source/" + file.relative_to(root).as_posix())
print("Final delivery:")
for file in sorted(destination.iterdir()):
    print(file.name, file.stat().st_size if file.is_file() else "7 PNG files")
