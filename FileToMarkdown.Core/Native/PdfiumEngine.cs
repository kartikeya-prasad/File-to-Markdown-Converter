using System.Runtime.InteropServices;

namespace FileToMarkdown.Core.Native;

/// <summary>
/// Managed wrapper over the minimal PDFium surface: detect a page's text layer and
/// rasterize pages to BGRA bitmaps for OCR. PDFium is not thread-safe per document,
/// so calls are serialized with a per-document lock.
/// </summary>
public sealed class PdfiumEngine : IDisposable
{
    private static readonly object _initLock = new();
    private static bool _initialized;
    private static int _instanceCount;

    public static void EnsureInitialized()
    {
        lock (_initLock)
        {
            if (!_initialized)
            {
                PdfiumNative.FPDF_InitLibrary();
                _initialized = true;
            }
            _instanceCount++;
        }
    }

    public static void Shutdown()
    {
        lock (_initLock)
        {
            _instanceCount--;
            if (_instanceCount <= 0 && _initialized)
            {
                PdfiumNative.FPDF_DestroyLibrary();
                _initialized = false;
                _instanceCount = 0;
            }
        }
    }

    private IntPtr _document;
    private readonly object _docLock = new();
    private bool _disposed;

    public int PageCount { get; private set; }

    public PdfiumEngine() => EnsureInitialized();

    public void LoadDocument(string filePath, string? password = null)
    {
        lock (_docLock)
        {
            CloseDocumentInternal();
            _document = PdfiumNative.FPDF_LoadDocument(filePath, password);
            if (_document == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"PDFium failed to load '{filePath}' (error {PdfiumNative.FPDF_GetLastError()}).");
            PageCount = PdfiumNative.FPDF_GetPageCount(_document);
        }
    }

    public (double Width, double Height) GetPageSize(int pageIndex)
    {
        lock (_docLock)
        {
            ThrowIfDisposed();
            var page = PdfiumNative.FPDF_LoadPage(_document, pageIndex);
            if (page == IntPtr.Zero) throw new InvalidOperationException("Failed to load page.");
            try
            {
                return (PdfiumNative.FPDF_GetPageWidth(page), PdfiumNative.FPDF_GetPageHeight(page));
            }
            finally { PdfiumNative.FPDF_ClosePage(page); }
        }
    }

    /// <summary>Extracts the page's existing (embedded) text layer, if any.</summary>
    public string ExtractText(int pageIndex)
    {
        lock (_docLock)
        {
            ThrowIfDisposed();
            var page = PdfiumNative.FPDF_LoadPage(_document, pageIndex);
            if (page == IntPtr.Zero) return string.Empty;
            try
            {
                var textPage = PdfiumNative.FPDFText_LoadPage(page);
                if (textPage == IntPtr.Zero) return string.Empty;
                try
                {
                    int charCount = PdfiumNative.FPDFText_CountChars(textPage);
                    if (charCount <= 0) return string.Empty;

                    int bufferSize = (charCount + 1) * 2; // UTF-16 + null terminator
                    IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
                    try
                    {
                        PdfiumNative.FPDFText_GetText(textPage, 0, charCount, buffer);
                        return Marshal.PtrToStringUni(buffer) ?? string.Empty;
                    }
                    finally { Marshal.FreeHGlobal(buffer); }
                }
                finally { PdfiumNative.FPDFText_ClosePage(textPage); }
            }
            finally { PdfiumNative.FPDF_ClosePage(page); }
        }
    }

    /// <summary>
    /// Renders a page to a 32-bpp BGRA bitmap at the given pixel size (white background).
    /// </summary>
    public (byte[] Bgra, int Width, int Height, int Stride) RenderPageBgra(int pageIndex, int width, int height)
    {
        lock (_docLock)
        {
            ThrowIfDisposed();
            var page = PdfiumNative.FPDF_LoadPage(_document, pageIndex);
            if (page == IntPtr.Zero) throw new InvalidOperationException("Failed to load page.");
            try
            {
                int stride = width * 4;
                IntPtr buffer = Marshal.AllocHGlobal(stride * height);
                try
                {
                    var bitmap = PdfiumNative.FPDFBitmap_CreateEx(
                        width, height, PdfiumNative.FPDFBitmap_BGRA, buffer, stride);
                    if (bitmap == IntPtr.Zero) throw new InvalidOperationException("Failed to create bitmap.");
                    try
                    {
                        PdfiumNative.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
                        PdfiumNative.FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, 0,
                            PdfiumNative.FPDF_ANNOT | PdfiumNative.FPDF_LCD_TEXT);

                        var managed = new byte[stride * height];
                        Marshal.Copy(buffer, managed, 0, managed.Length);
                        return (managed, width, height, stride);
                    }
                    finally { PdfiumNative.FPDFBitmap_Destroy(bitmap); }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            finally { PdfiumNative.FPDF_ClosePage(page); }
        }
    }

    private void CloseDocumentInternal()
    {
        if (_document != IntPtr.Zero)
        {
            PdfiumNative.FPDF_CloseDocument(_document);
            _document = IntPtr.Zero;
            PageCount = 0;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _document == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(PdfiumEngine));
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_docLock)
        {
            CloseDocumentInternal();
            _disposed = true;
        }
        Shutdown();
    }
}
