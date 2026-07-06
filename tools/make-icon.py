"""Generates the app's raster branding from Assets/logo.svg:

  * FileToMarkdown.App/Assets/app.ico - multi-resolution icon for the exe,
    taskbar, shortcuts and Explorer (each frame rendered at native size).
  * packaging/msix/Assets/*.png       - MSIX tile/logo images.

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


# MSIX visual assets: square logos are direct renders; wide/splash center the
# logo on a transparent canvas.
MSIX_ASSETS = {
    "Square44x44Logo.png": (44, 44),
    "Square44x44Logo.targetsize-24_altform-unplated.png": (24, 24),
    "Square150x150Logo.png": (150, 150),
    "StoreLogo.png": (50, 50),
    "Wide310x150Logo.png": (310, 150),
    "SplashScreen.png": (620, 300),
}


def make_msix_assets():
    out_dir = repo / "packaging" / "msix" / "Assets"
    out_dir.mkdir(parents=True, exist_ok=True)
    for name, (w, h) in MSIX_ASSETS.items():
        if w == h:
            img = render(w)
        else:
            logo = render(int(h * 0.8))
            img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            img.paste(logo, ((w - logo.width) // 2, (h - logo.height) // 2), logo)
        img.save(out_dir / name)
    print(f"Wrote {len(MSIX_ASSETS)} MSIX assets to {out_dir}")


def main():
    frames = [render(s) for s in SIZES]
    frames[0].save(
        ico,
        format="ICO",
        sizes=[(s, s) for s in SIZES],
        append_images=frames[1:],
    )
    print(f"Wrote {ico} ({ico.stat().st_size} bytes) with sizes {SIZES}")

    make_msix_assets()

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
