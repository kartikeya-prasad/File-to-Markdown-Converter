# File to Markdown Converter

A native **Windows (WinUI 3)** desktop app that batch-converts many file types into Markdown (`.md`) files.

It is built on Microsoft's [markitdown](https://github.com/microsoft/markitdown) conversion engine, with one important change: instead of markitdown's **LLM**-based image description, this app uses **Tesseract OCR** to extract real text from images and scanned PDFs — fully offline.

## What it does

- **Batch convert** a queue of files (or a whole folder) to `.md`.
- **Documents** — PDF, Word, PowerPoint, Excel, HTML, CSV/JSON/XML, EPub, Outlook `.msg`, ZIP — via markitdown.
- **Images** (PNG/JPG/TIFF/BMP/GIF/WEBP) — text extracted with **Tesseract OCR**.
- **Scanned / image-only PDFs** — pages rendered with PDFium, then OCR'd with Tesseract.
- **Audio & YouTube** — transcription via markitdown (requires internet).
- **Light / Dark / System** theme.

## Architecture

| Project | Role |
|---|---|
| `FileToMarkdown.App` | WinUI 3 shell — batch queue UI, drag-drop, pickers, settings. |
| `FileToMarkdown.Core` | Conversion engine — Python/markitdown runner, Tesseract OCR, PDFium rasterizer, file routing. |

The app ships a **private, embedded Python 3.13** runtime (markitdown does not yet install on Python 3.14) and calls `markitdown` as a subprocess via a persistent worker. No system Python is required.

## Prerequisites (development)

- .NET SDK 9+ • WinApp CLI • WinUI 3 templates • Developer Mode (for running)
- After cloning, run `tools/setup-python.ps1` once to download the embedded Python runtime, `markitdown[all]`, Tesseract `tessdata`, and `ffmpeg`. These are **not** committed (see `.gitignore`) to keep the repo small.

## Build & run

```powershell
dotnet build FileToMarkdown.App\FileToMarkdown.App.csproj -c Debug -p:Platform=x64
```

## License

TBD.
