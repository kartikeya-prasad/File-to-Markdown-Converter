using System.Runtime.InteropServices;

namespace FileToMarkdown.Core.Native;

/// <summary>
/// Minimal P/Invoke surface for the PDFium native library — only what is needed to
/// detect a PDF's text layer and rasterize pages for OCR. PDFium is Google's
/// open-source PDF engine (BSD), supplied by the bblanchon.PDFium.Win32 package.
/// (Adapted from the VelocityPDF interop.)
/// </summary>
public static partial class PdfiumNative
{
    private const string PdfiumDll = "pdfium";

    // ── Library lifecycle ────────────────────────────────────────────────
    [LibraryImport(PdfiumDll)]
    public static partial void FPDF_InitLibrary();

    [LibraryImport(PdfiumDll)]
    public static partial void FPDF_DestroyLibrary();

    [LibraryImport(PdfiumDll)]
    public static partial uint FPDF_GetLastError();

    // ── Document operations ──────────────────────────────────────────────
    [LibraryImport(PdfiumDll, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr FPDF_LoadDocument(string filePath, string? password);

    [LibraryImport(PdfiumDll)]
    public static partial void FPDF_CloseDocument(IntPtr document);

    [LibraryImport(PdfiumDll)]
    public static partial int FPDF_GetPageCount(IntPtr document);

    // ── Page operations ──────────────────────────────────────────────────
    [LibraryImport(PdfiumDll)]
    public static partial IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

    [LibraryImport(PdfiumDll)]
    public static partial void FPDF_ClosePage(IntPtr page);

    [LibraryImport(PdfiumDll)]
    public static partial double FPDF_GetPageWidth(IntPtr page);

    [LibraryImport(PdfiumDll)]
    public static partial double FPDF_GetPageHeight(IntPtr page);

    // ── Rendering ────────────────────────────────────────────────────────
    [LibraryImport(PdfiumDll)]
    public static partial IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);

    [LibraryImport(PdfiumDll)]
    public static partial void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [LibraryImport(PdfiumDll)]
    public static partial void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page,
        int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [LibraryImport(PdfiumDll)]
    public static partial void FPDFBitmap_Destroy(IntPtr bitmap);

    // ── Text extraction ──────────────────────────────────────────────────
    [LibraryImport(PdfiumDll)]
    public static partial IntPtr FPDFText_LoadPage(IntPtr page);

    [LibraryImport(PdfiumDll)]
    public static partial void FPDFText_ClosePage(IntPtr textPage);

    [LibraryImport(PdfiumDll)]
    public static partial int FPDFText_CountChars(IntPtr textPage);

    [LibraryImport(PdfiumDll)]
    public static partial int FPDFText_GetText(IntPtr textPage, int startIndex, int count, IntPtr result);

    [LibraryImport(PdfiumDll)]
    public static partial int FPDFText_CountRects(IntPtr textPage, int startIndex, int count);

    [LibraryImport(PdfiumDll)]
    public static partial int FPDFText_GetRect(IntPtr textPage, int rectIndex,
        out double left, out double top, out double right, out double bottom);

    // ── Render flags / bitmap formats ────────────────────────────────────
    public const int FPDF_ANNOT = 0x01;
    public const int FPDF_LCD_TEXT = 0x02;
    public const int FPDFBitmap_BGRA = 4;
}
