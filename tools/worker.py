"""
Persistent markitdown worker for File to Markdown Converter.

Runs as a long-lived child process of the .NET app. markitdown (and its heavy
deps: magika/onnxruntime) are imported ONCE at startup; thereafter each line on
stdin is a JSON conversion request, answered with one JSON line on stdout. This
avoids paying interpreter + import startup (~1-2s) on every file in a batch.

Protocol (one JSON object per line, UTF-8):
  request : {"id": <int>, "source": "<path-or-url>"}
  ready   : {"ready": true, "version": "<markitdown version or ''>"}   (once, at start)
  reply   : {"id": <int>, "ok": true,  "markdown": "<text>"}
            {"id": <int>, "ok": false, "error": "<message>"}

Any stray output from libraries is redirected to stderr so the protocol channel
(real stdout) stays clean.
"""
import sys
import io
import os
import json

# Force UTF-8 on the protocol channel.
_real_stdout = sys.stdout
try:
    _real_stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", newline="\n")
except Exception:
    pass

# Redirect anything libraries print to stdout onto stderr, so it can't corrupt
# our JSON protocol.
sys.stdout = sys.stderr


def _emit(obj):
    _real_stdout.write(json.dumps(obj, ensure_ascii=False))
    _real_stdout.write("\n")
    _real_stdout.flush()


def main():
    # Make a bundled ffmpeg (next to this interpreter) visible to pydub/markitdown.
    here = os.path.dirname(os.path.abspath(sys.executable))
    os.environ["PATH"] = here + os.pathsep + os.environ.get("PATH", "")

    try:
        from markitdown import MarkItDown
    except Exception as e:  # pragma: no cover
        _emit({"ready": False, "error": f"failed to import markitdown: {e}"})
        return

    try:
        import markitdown as _m
        version = getattr(_m, "__version__", "")
    except Exception:
        version = ""

    md = MarkItDown(enable_plugins=False)
    _emit({"ready": True, "version": version})

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
        except Exception as e:
            _emit({"id": None, "ok": False, "error": f"bad request json: {e}"})
            continue

        rid = req.get("id")
        if req.get("cmd") == "shutdown":
            break

        source = req.get("source")
        if not source:
            _emit({"id": rid, "ok": False, "error": "missing 'source'"})
            continue

        try:
            result = md.convert(source)
            _emit({"id": rid, "ok": True, "markdown": result.text_content})
        except Exception as e:
            _emit({"id": rid, "ok": False, "error": f"{type(e).__name__}: {e}"})


if __name__ == "__main__":
    main()
