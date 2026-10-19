"""Render the editable SVG source through the installed Chrome browser."""
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / ".preview"
OUTPUT.mkdir(exist_ok=True)
with sync_playwright() as p:
    browser = p.chromium.launch(executable_path="C:/Program Files/Google/Chrome/Application/chrome.exe", headless=True)
    page = browser.new_page(viewport={"width":1280,"height":720}, device_scale_factor=1)
    for svg in sorted((ROOT / "svg_output").glob("*.svg")):
        page.goto("http://127.0.0.1:6060/svg_output/"+svg.name)
        page.wait_for_timeout(500)
        page.screenshot(path=str(OUTPUT/(svg.stem+".png")))
        print(svg.stem, flush=True)
    browser.close()
