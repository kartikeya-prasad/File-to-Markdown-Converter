# Contributing

Thanks for taking an interest! This project is a WinUI 3 (.NET 9) Windows app with a
C# conversion engine and a bundled Python runtime for Microsoft's markitdown.

## Getting a build running

Windows 10/11 x64 is required to build the app (the `net9.0-windows` targets do not
compile on Linux/macOS).

```powershell
git clone https://github.com/kartikeya-prasad/File-to-Markdown-Converter.git
cd File-to-Markdown-Converter
powershell -File tools\setup-python.ps1      # fetch bundled Python + markitdown + tessdata
dotnet build FileToMarkdown.sln -c Debug -p:Platform=x64
dotnet test tests\FileToMarkdown.Core.Tests -c Debug -p:Platform=x64
```

Python worker tests run anywhere:

```bash
pip install pytest pdfminer.six
pytest tests/python
```

## Project layout

| Path | What lives there |
|---|---|
| `FileToMarkdown.App` | WinUI 3 shell — MVVM (CommunityToolkit), DI, theming, updater UI |
| `FileToMarkdown.Core` | Conversion engine — routing, per-page PDF pipeline, OCR engines, updater logic |
| `tools/` | Python workers, build/packaging scripts, `versions.json` tool pins |
| `tests/` | xUnit tests (Core) and pytest tests (Python workers) |
| `installer/` | Inno Setup and WiX definitions |

## Conventions

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org)
  (`feat:`, `fix:`, `ci:`, `docs:`, `chore:`, …). Release notes are generated
  from these, so the prefix matters.
- **Changelog:** add a bullet to the top section of `CHANGELOG.md`. The release
  workflow extracts the `## v<version>` section matching the tag as the release
  body — keep that heading format exactly.
- **Tests:** decision logic belongs in pure classes in Core with xUnit coverage
  (see `PdfPageClassifier`/`SparseTextMerger` for the pattern). Python protocol
  logic gets pytest coverage that must not require torch/markitdown installed.
- **CI:** every push/PR runs `build-and-test.yml` (Windows build + tests, Ubuntu
  pytest). Keep it green — for contributors without a Windows machine, CI is the
  compiler.

## Releases

Maintainers cut releases by pushing a tag (`git tag v0.x.y && git push origin v0.x.y`)
or dispatching the **Build & Release** workflow. It builds the portable zip, the
Inno Setup installer and the MSI, generates `SHA256SUMS.txt`, and publishes a GitHub
Release; installed apps pick it up through the in-app updater.

### Signing (future work)

Artifacts are currently unsigned; MSIX packaging is parked until a code-signing
certificate (e.g. Azure Trusted Signing) is set up. Store certificates and
passwords only in GitHub Actions secrets — never in the repository.
