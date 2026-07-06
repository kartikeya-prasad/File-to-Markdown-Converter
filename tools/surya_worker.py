"""
Persistent Surya OCR worker for File to Markdown Converter.

Same line-delimited JSON protocol as worker.py, specialized for OCR of image
files (the .NET side rasterizes PDF pages to PNGs itself):

  request : {"id": <int>, "path": "<image file>"}
            {"cmd": "shutdown"}
  ready   : {"ready": true, "version": "<surya version or ''>"}     (once)
  status  : {"status": "loading_models"}                            (before ready)
  reply   : {"id": <int>, "ok": true,  "text": "<recognized text>"}
            {"id": <int>, "ok": false, "error": "<message>"}

Surya (surya-ocr on PyPI) is installed on demand by the app - it pulls PyTorch
(~2 GB) and downloads model weights from Hugging Face on first inference.
"""
import sys
import io
import json

_real_stdout = sys.stdout


def _setup_io():
    """Claim stdout for the JSON protocol (UTF-8), routing library prints to stderr."""
    global _real_stdout
    try:
        _real_stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", newline="\n")
    except Exception:
        pass
    sys.stdout = sys.stderr


def _emit(obj):
    _real_stdout.write(json.dumps(obj, ensure_ascii=False))
    _real_stdout.write("\n")
    _real_stdout.flush()


def handle_request(line, ocr_fn):
    """Parses one protocol line and returns the reply dict (None = shutdown).

    Pure protocol logic, separated from model code so it is unit-testable
    without surya/torch installed. ocr_fn(path) -> recognized text.
    """
    line = line.strip()
    if not line:
        return {}
    try:
        req = json.loads(line)
    except Exception as e:
        return {"id": None, "ok": False, "error": f"bad request json: {e}"}

    if req.get("cmd") == "shutdown":
        return None

    rid = req.get("id")
    path = req.get("path")
    if not path:
        return {"id": rid, "ok": False, "error": "missing 'path'"}

    try:
        return {"id": rid, "ok": True, "text": ocr_fn(path)}
    except Exception as e:
        return {"id": rid, "ok": False, "error": f"{type(e).__name__}: {e}"}


def _load_ocr():
    """Loads surya predictors, tolerating API differences across versions."""
    _emit({"status": "loading_models"})

    from PIL import Image

    try:
        # surya >= 0.14: recognition takes a foundation predictor.
        from surya.foundation import FoundationPredictor
        from surya.recognition import RecognitionPredictor
        from surya.detection import DetectionPredictor

        recognition = RecognitionPredictor(FoundationPredictor())
        detection = DetectionPredictor()
    except Exception:
        from surya.recognition import RecognitionPredictor
        from surya.detection import DetectionPredictor

        recognition = RecognitionPredictor()
        detection = DetectionPredictor()

    def ocr(path):
        image = Image.open(path).convert("RGB")
        try:
            predictions = recognition([image], det_predictor=detection)
        except TypeError:
            # Older call signature: (images, langs, det_predictor)
            predictions = recognition([image], [None], detection)
        page = predictions[0]
        lines = getattr(page, "text_lines", None) or []
        return "\n".join((line.text or "") for line in lines).strip()

    return ocr


def main():
    _setup_io()
    try:
        ocr = _load_ocr()
    except Exception as e:
        _emit({"ready": False, "error": f"failed to load surya: {e}"})
        return

    try:
        import surya

        version = getattr(surya, "__version__", "")
    except Exception:
        version = ""

    _emit({"ready": True, "version": version})

    for line in sys.stdin:
        reply = handle_request(line, ocr)
        if reply is None:
            break
        if reply:
            _emit(reply)


if __name__ == "__main__":
    main()
