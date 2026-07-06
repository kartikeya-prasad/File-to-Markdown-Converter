"""Checks whether the pinned bundled-tool versions in tools/versions.json are stale.

Currently tracks markitdown (the fast-moving dependency) on PyPI; the embedded
Python version is bumped manually because new interpreter releases need a
compatibility check with markitdown's dependency tree first.

Updates versions.json in place when something is newer and prints a summary.
The weekly tool-updates workflow turns any resulting diff into a pull request,
so the next tagged release - and, through the in-app updater, every user -
picks the update up automatically.

Usage: python tools/check-tool-updates.py
Exit code 0 always (a non-zero exit would fail the scheduled workflow run).
"""
import json
import urllib.request
from pathlib import Path

VERSIONS = Path(__file__).resolve().parent / "versions.json"


def pypi_latest(package):
    url = f"https://pypi.org/pypi/{package}/json"
    with urllib.request.urlopen(url, timeout=30) as response:
        return json.load(response)["info"]["version"]


def parse(version):
    try:
        return tuple(int(p) for p in version.split("."))
    except ValueError:
        return (0,)


def main():
    data = json.loads(VERSIONS.read_text(encoding="utf-8"))
    changes = []

    latest_markitdown = pypi_latest("markitdown")
    if parse(latest_markitdown) > parse(data["markitdown"]):
        changes.append(f"markitdown {data['markitdown']} -> {latest_markitdown}")
        data["markitdown"] = latest_markitdown

    if changes:
        VERSIONS.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
        print("Updated versions.json:")
        for change in changes:
            print(f"  {change}")
    else:
        print("All pinned tool versions are current.")


if __name__ == "__main__":
    main()
