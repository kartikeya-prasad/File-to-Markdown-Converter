"""Generates FileToMarkdown.App/Assets/app.ico from Assets/logo.svg.

Each frame is rendered from the SVG at its native size (no downscaling blur),
then packed into one multi-resolution .ico. Windows picks the right frame for
the exe, taskbar, Start Menu, desktop shortcut, and Explorer views.

Requires: pip install cairosvg pillow
Usage:    python tools/make-icon.py [preview.png]
"""
import io
import sys
from pathlib import Path

import cairosvg
from PIL import Image

SIZES = [256, 128, 96, 64, 48, 40, 32, 24, 20, 16]

repo = Path(__file__).resolve().parents[1]
svg = repo / "FileToMarkdown.App" / "Assets" / "logo.svg"
ico = repo / "FileToMarkdown.App" / "Assets" / "app.ico"


def render(size):
    png = cairosvg.svg2png(url=str(svg), output_width=size, output_height=size)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def main():
    frames = [render(s) for s in SIZES]
    frames[0].save(
        ico,
        format="ICO",
        sizes=[(s, s) for s in SIZES],
        append_images=frames[1:],
    )
    print(f"Wrote {ico} ({ico.stat().st_size} bytes) with sizes {SIZES}")

    # Side-by-side preview strip of the small sizes for manual inspection.
    preview_sizes = [16, 20, 24, 32, 48]
    pad = 8
    strip = Image.new(
        "RGBA",
        (sum(s + pad for s in preview_sizes) + pad, max(preview_sizes) + 2 * pad),
        (240, 240, 240, 255),
    )
    x = pad
    for s in preview_sizes:
        img = render(s)
        strip.paste(img, (x, pad + (max(preview_sizes) - s) // 2), img)
        x += s + pad
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else repo / "icon-preview.png"
    strip.save(out)
    print(f"Preview strip: {out}")


if __name__ == "__main__":
    main()
