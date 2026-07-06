"""Protocol tests for tools/surya_worker.py.

handle_request is pure protocol logic, so these run without surya/torch:
the OCR callable is faked.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))

import surya_worker  # noqa: E402


def ok_ocr(path):
    return f"text from {path}"


def boom_ocr(path):
    raise RuntimeError("model exploded")


def test_valid_request_returns_text():
    reply = surya_worker.handle_request(
        json.dumps({"id": 7, "path": "page.png"}), ok_ocr)
    assert reply == {"id": 7, "ok": True, "text": "text from page.png"}


def test_shutdown_returns_none():
    assert surya_worker.handle_request(json.dumps({"cmd": "shutdown"}), ok_ocr) is None


def test_blank_line_is_ignored():
    assert surya_worker.handle_request("   \n", ok_ocr) == {}


def test_bad_json_reports_error():
    reply = surya_worker.handle_request("{not json", ok_ocr)
    assert reply["ok"] is False
    assert "bad request json" in reply["error"]


def test_missing_path_reports_error():
    reply = surya_worker.handle_request(json.dumps({"id": 3}), ok_ocr)
    assert reply == {"id": 3, "ok": False, "error": "missing 'path'"}


def test_ocr_exception_is_reported_not_raised():
    reply = surya_worker.handle_request(
        json.dumps({"id": 4, "path": "x.png"}), boom_ocr)
    assert reply["id"] == 4
    assert reply["ok"] is False
    assert "model exploded" in reply["error"]
