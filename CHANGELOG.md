# Changelog

All notable changes to this project are documented here. The release workflow
reads the section matching the version being built and uses it as the GitHub
Release description, so keep the newest version at the top in the format below.

## v0.5.0 — 2026-07-07

### Fixed
- **v0.3.0 did not launch at all** (any install type). The v0.3.0 build was
  missing `FileToMarkdown.App.pri` — the resource index holding the app's
  compiled XAML — because the MSIX cleanup removed `EnableMsixTooling`, which
  (despite its name) also drives that file's generation for unpackaged builds.
  The flag is restored with a warning comment, and the release build now fails
  hard if the `.pri` is ever missing again, so a dead build can't ship.

### Removed
- **MSIX package and its self-signed certificate flow** (introduced in
  v0.3.0). The one-time certificate import was more friction than value;
  the portable zip, `Setup.exe`, and `.msi` remain.

### Notes
- Versions 0.3.x/0.4.x are skipped; this release supersedes the broken
  v0.3.0. All v0.3.0 features (per-page hybrid PDF OCR, Surya engine,
  Enhanced PDF OCR, auto-updates, new theme and icon) are included and
  unchanged.

## v0.3.0 — 2026-07-06

### Fixed
- **Mixed scanned/digital PDFs convert correctly.** The old whole-file text
  probe treated scans with machine-typed page numbers as digital documents and
  skipped OCR entirely. PDFs are now analyzed page by page: digital pages keep
  their extracted text, scanned pages are OCR'd, and sparse stamped text (page
  numbers, headers) is merged back in without loss or duplication.
- The OCR language setting now actually reaches the OCR engine.

### Added
- **In-app auto-updates** from GitHub Releases: checked on every launch plus a
  manual "Check for updates" in Settings, SHA-256 verification of the
  downloaded installer, and per-version skip. Releases now publish
  `SHA256SUMS.txt`.
- **MSIX package** built and signed in CI on every release. Signed with a
  self-signed certificate (public `.cer` published alongside for a one-time
  Trusted People import); automatically switches to a real certificate if
  signing secrets are configured.
- **Selectable OCR engine:** built-in Tesseract (default) or Surya — much
  better table/layout fidelity — installed on demand (~2 GB) with a guided,
  cancellable download inside Settings.
- **Enhanced PDF OCR component (optional):** on-demand install of OCRmyPDF +
  Tesseract CLI + Ghostscript. PDFs then run through `--redo-ocr` for a proper
  invisible text layer, and a new toggle saves a searchable `name.ocr.pdf`
  next to the Markdown.
- **App icon everywhere:** a multi-resolution icon generated from the logo is
  now embedded in the exe and used by the window, taskbar, installer wizard,
  Start Menu and desktop shortcuts, and Add/Remove Programs.
- Warm greige design system (light/dark/system) with tokenized colors and a
  Mica backdrop; live OS theme following.
- CI on every push/PR (Windows build + xUnit, Ubuntu pytest), a unit-test
  project, and a weekly workflow that opens PRs when bundled tools
  (markitdown & friends) have new releases.
- MIT license, CONTRIBUTING guide, and a reworked README.

### Changed
- With the Surya engine selected, PDF Markdown always comes from Surya's
  per-page pipeline (best table fidelity); OCRmyPDF is then used only to
  produce the optional searchable PDF instead of overriding the engine choice.
- On-demand Python packages (Surya, OCRmyPDF) now install into a per-user
  directory, so they work for MSIX and per-machine installs where the app
  folder is read-only.
- Bundled tool versions are pinned in `tools/versions.json` for reproducible
  release builds (previously "latest at build time").
- Release notes are now hybrid: the curated section from this file plus
  GitHub's auto-generated categorized commit notes.
- Replaced the unused MSIX template leftovers with a real CI-built MSIX
  package (see Added).

## v0.2.0 — 2026-06-08

### Fixed
- **The app now actually launches.** The v0.1.0 `.exe`/`.msi`/portable builds
  could crash on startup because the publish profiles enabled IL trimming
  (unsupported by WinUI 3) and did not bundle the Windows App SDK runtime. Both
  are fixed: trimming is off and the runtime is now self-contained, so the app
  starts on a clean machine with nothing pre-installed.

### Added
- Startup/runtime crashes are now written to
  `%LOCALAPPDATA%\FileToMarkdownConverter\crash.log` so failures are
  diagnosable instead of silent.

### Changed
- Releases are now built and published automatically by GitHub Actions on a
  clean Windows runner (no more hand-built artifacts). The MSI version is
  stamped from the release version.

### Notes
- No changes to the conversion features themselves, and nothing was removed —
  this release is purely the launch fix plus release tooling.

## v0.1.0 — 2026-06-03

### Added
- Initial release: native WinUI 3 app that batch-converts documents, images,
  and scanned PDFs to Markdown fully offline, using markitdown + Tesseract OCR.
  Ships as a portable zip, an Inno Setup `.exe`, and a WiX `.msi`.
