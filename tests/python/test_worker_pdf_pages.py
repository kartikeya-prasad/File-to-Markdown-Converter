"""Tests for the pdf_pages command of tools/worker.py.

Verifies the 0-based page-index mapping: pdfminer numbers filtered pages 1..n,
so worker._pdf_pages must map yielded pages back to the originally requested
document indices.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))

import worker  # noqa: E402


def make_pdf(path, page_texts):
    """Write a minimal valid PDF; each entry in page_texts becomes one page."""
    objects = []
    n = len(page_texts)
    kids = " ".join(f"{4 + 2 * i} 0 R" for i in range(n))
    objects.append("<< /Type /Catalog /Pages 2 0 R >>")
    objects.append(f"<< /Type /Pages /Kids [{kids}] /Count {n} >>")
    objects.append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
    for i, text in enumerate(page_texts):
        objects.append(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
            f"/Resources << /Font << /F1 3 0 R >> >> /Contents {5 + 2 * i} 0 R >>"
        )
        if text:
            esc = text.replace("\\", r"\\").replace("(", r"\(").replace(")", r"\)")
            content = f"BT /F1 14 Tf 72 720 Td ({esc}) Tj ET"
        else:
            content = ""
        objects.append(f"<< /Length {len(content)} >>\nstream\n{content}\nendstream")

    out = "%PDF-1.4\n"
    offsets = []
    for i, obj in enumerate(objects):
        offsets.append(len(out))
        out += f"{i + 1} 0 obj\n{obj}\nendobj\n"
    xref = len(out)
    out += f"xref\n0 {len(objects) + 1}\n0000000000 65535 f \n"
    for off in offsets:
        out += f"{off:010d} 00000 n \n"
    out += f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"
    path.write_bytes(out.encode("ascii"))
    return path


def test_pdf_pages_maps_to_original_indices(tmp_path):
    pdf = make_pdf(tmp_path / "t.pdf", ["alpha page", "beta page", "gamma page"])

    pages = worker._pdf_pages(str(pdf), [0, 2])

    assert set(pages.keys()) == {"0", "2"}
    assert "alpha" in pages["0"]
    assert "gamma" in pages["2"]
    assert "beta" not in pages["0"] + pages["2"]


def test_pdf_pages_unsorted_request_still_maps_correctly(tmp_path):
    pdf = make_pdf(tmp_path / "t.pdf", ["alpha", "beta", "gamma"])

    pages = worker._pdf_pages(str(pdf), [2, 0])

    assert "alpha" in pages["0"]
    assert "gamma" in pages["2"]


def test_pdf_pages_empty_page_yields_empty_text(tmp_path):
    pdf = make_pdf(tmp_path / "t.pdf", ["only text page", None])

    pages = worker._pdf_pages(str(pdf), [1])

    assert pages.get("1", "") == ""
