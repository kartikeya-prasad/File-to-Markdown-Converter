# Changelog

All notable changes to this project are documented here. The release workflow
reads the section matching the version being built and uses it as the GitHub
Release description, so keep the newest version at the top in the format below.

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
