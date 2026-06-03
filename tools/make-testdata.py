"""Generate sample inputs for validating the conversion engine."""
import sys, os
from pathlib import Path

out = Path(sys.argv[1] if len(sys.argv) > 1 else "testdata")
out.mkdir(parents=True, exist_ok=True)

# --- markitdown paths: xlsx, html, csv ------------------------------------
from openpyxl import Workbook
wb = Workbook(); ws = wb.active; ws.title = "Scores"
ws.append(["Name", "Score"]); ws.append(["Alice", 90]); ws.append(["Bob", 85])
wb.save(out / "sheet.xlsx")

(out / "page.html").write_text(
    "<html><body><h1>Title</h1><p>Hello <b>world</b>.</p>"
    "<ul><li>one</li><li>two</li></ul></body></html>", encoding="utf-8")

(out / "data.csv").write_text("name,score\nAlice,90\nBob,85\n", encoding="utf-8")

# --- OCR paths: image with text, and an image-only (scanned) PDF ----------
from PIL import Image, ImageDraw, ImageFont
try:
    font = ImageFont.truetype("arial.ttf", 48)
except Exception:
    font = ImageFont.load_default()

img = Image.new("RGB", (900, 250), "white")
d = ImageDraw.Draw(img)
d.text((30, 90), "Hello OCR 12345", fill="black", font=font)
img.save(out / "image-text.png")
# Save the scanned PDF as a bilevel image so Pillow uses CCITT (no JPEG codec needed).
img.convert("1").save(out / "scanned.pdf")   # image-only PDF -> scanned path

print("Wrote test data to", out.resolve())
for p in sorted(out.iterdir()):
    print("  ", p.name)
