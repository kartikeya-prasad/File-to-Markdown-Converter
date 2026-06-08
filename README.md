<div align="center">

<img src="FileToMarkdown.App/Assets/logo.svg" width="96" height="96" alt="File to Markdown Converter logo" />

# File to Markdown Converter

**A native Windows app that batch-converts documents, images and scanned PDFs into Markdown — fully offline.**

</div>

---

> ### ⚠️ This app is **completely vibe coded using Claude**. So the risk is yours!
> This is just a fun side project by a **non-programmer**. There are no guarantees — review the code and use at your own risk.

---

It's built on Microsoft's [markitdown](https://github.com/microsoft/markitdown) conversion engine, with one key change: instead of markitdown's **LLM**-based image description, it uses **Tesseract OCR** to pull real text out of images and scanned PDFs — no cloud, no API keys.

## ✨ What it does

- **Batch convert** a queue of files (or whole folders) to `.md` — drag & drop, file picker, or folder picker.
- **Documents** → PDF, Word, PowerPoint, Excel, HTML, CSV/JSON/XML, EPub, Outlook `.msg`, ZIP (via markitdown).
- **Images** (PNG/JPG/TIFF/BMP/GIF/WEBP) → text extracted with **Tesseract OCR**.
- **Scanned / image-only PDFs** → pages rendered with PDFium, then OCR'd.
- **Audio & YouTube** → transcription via markitdown (these need internet).
- **Light / Dark / System** theme, configurable concurrency, OCR DPI, and overwrite policy.

## 📦 Download

Grab the latest build from the [**Releases**](../../releases) page:

| Artifact | What it is |
|---|---|
| `FileToMarkdownConverter-portable-x64.zip` | Portable — unzip anywhere and run `FileToMarkdown.App.exe`. No install. |
| `FileToMarkdownConverter-Setup.exe` | Friendly installer (Inno Setup). |
| `FileToMarkdownConverter.msi` | MSI installer (WiX). |

All artifacts are **self-contained** — no need to install .NET, the Windows App SDK, or Python. The Python 3.13 runtime, markitdown, ffmpeg, and Tesseract data are bundled inside.

> A true single-file `.exe` isn't possible here: WinUI 3 + native PDFium/Tesseract libraries + the embedded Python bundle can't be packed into one file. "Portable" means an xcopy-deployable folder you launch by its `.exe`.

## 🧠 How it works

| Project | Role |
|---|---|
| `FileToMarkdown.App` | WinUI 3 desktop shell — batch queue, drag-drop, pickers, settings, theme. |
| `FileToMarkdown.Core` | Conversion engine — markitdown runner, Tesseract OCR, PDFium rasterizer, routing. |

- A **persistent Python worker** (`tools/worker.py`) imports markitdown once and converts files over a stdin/stdout protocol, so a batch doesn't pay interpreter startup per file.
- **Why a bundled Python 3.13?** markitdown doesn't install on Python 3.14 yet (its `magika` → `onnxruntime` dependency has no 3.14 wheels). The app ships its own private 3.13 so it works regardless of what's on your machine.
- `FileRouter` decides per file: images → OCR, PDFs → text-layer probe (born-digital → markitdown, scanned → OCR), everything else → markitdown.

## 🔁 Reproduce / build from source

**Prerequisites:** Windows 10/11 (x64), [.NET SDK 9+](https://dotnet.microsoft.com/download), the WinApp CLI (`winget install Microsoft.WinAppCli`), and the WinUI templates (`dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates`). For installers: Inno Setup (`winget install JRSoftware.InnoSetup`) and WiX v5 (`dotnet tool install --global wix --version 5.0.2`).

```powershell
# 1. Clone
git clone https://github.com/kartikeya-prasad/File-to-Markdown-Converter.git
cd File-to-Markdown-Converter

# 2. Fetch the bundled runtime (Python 3.13 + markitdown[all] + ffmpeg + Tesseract data).
#    These are large (~hundreds of MB) and intentionally NOT committed; this script regenerates them.
powershell -File tools\setup-python.ps1

# 3. Build & run (debug)
dotnet build FileToMarkdown.App\FileToMarkdown.App.csproj -c Debug -p:Platform=x64
# launch the built exe under FileToMarkdown.App\bin\x64\Debug\...\win-x64\

# 4. (optional) Produce distributables into dist\
powershell -File tools\build-portable.ps1        # portable .zip (run first; stages the publish folder)
powershell -File tools\build-exe-installer.ps1   # -> FileToMarkdownConverter-Setup.exe
powershell -File tools\build-msi.ps1             # -> FileToMarkdownConverter.msi
```

The app locates its runtime (`python\`, `tessdata\`) by walking up from the executable, so a debug build works in place once `setup-python.ps1` has run.

> **Automated releases:** pushing a version tag (e.g. `git tag v0.2.0 && git push origin v0.2.0`) runs the `Build & Release` GitHub Actions workflow, which builds all three distributables on a Windows runner with the correct publish flags and attaches them to a new GitHub Release. You can also trigger a build manually from the **Actions** tab. Don't publish hand-built artifacts from Visual Studio's *Publish* — always use the scripts above or the workflow, so trimming stays off and the Windows App SDK runtime is bundled.

## ⚠️ Known limitations

- **Audio transcription** is best-effort (markitdown's backend may need internet/extra setup).
- **YouTube** conversion needs internet (transcript API).
- First run after cloning requires `setup-python.ps1` (downloads the runtime).
- x64 only.

## 📄 License

TBD. Bundles third-party components under their own licenses: markitdown (MIT), PDFium (BSD), Tesseract (Apache-2.0), ffmpeg (LGPL/GPL), Python (PSF).
