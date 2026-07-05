using System.Text;

namespace FileToMarkdown.Core.Tests;

/// <summary>
/// Builds tiny, valid PDFs entirely in-memory so the PDFium-backed pipeline can be
/// exercised without binary fixtures. Each entry in <c>pageTexts</c> becomes one page;
/// null/empty produces a blank page (the shape of a scanned page's text layer).
/// </summary>
internal static class TestPdf
{
    public static string CreateFile(string directory, string name, params string?[] pageTexts)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, Build(pageTexts));
        return path;
    }

    public static byte[] Build(params string?[] pageTexts)
    {
        int pageCount = pageTexts.Length;

        // Object layout: 1=catalog, 2=pages, 3=font, then per page i: 4+2i=page, 5+2i=content.
        var objects = new List<string>();

        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{4 + 2 * i} 0 R"));
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        for (int i = 0; i < pageCount; i++)
        {
            objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] " +
                        $"/Resources << /Font << /F1 3 0 R >> >> /Contents {5 + 2 * i} 0 R >>");

            string content = string.IsNullOrEmpty(pageTexts[i])
                ? ""
                : BuildTextContent(pageTexts[i]!);
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        int xrefStart = sb.Length;
        sb.Append($"xref\n0 {objects.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var off in offsets)
            sb.Append($"{off:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string BuildTextContent(string text)
    {
        // One line of text per newline, 14pt Helvetica from the top of the page.
        var sb = new StringBuilder("BT /F1 14 Tf 72 720 Td 18 TL\n");
        foreach (var line in text.Split('\n'))
            sb.Append($"({Escape(line)}) Tj T*\n");
        sb.Append("ET");
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("\\", @"\\").Replace("(", @"\(").Replace(")", @"\)");
}
