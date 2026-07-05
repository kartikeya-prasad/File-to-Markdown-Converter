using System.Text;

namespace FileToMarkdown.Core;

/// <summary>
/// Merges the sparse digital text of a scanned page (typically a machine-typed page
/// number or header) into the page's OCR output without duplicating anything: fragments
/// the OCR already recognized (exactly or near enough) are dropped, fragments the OCR
/// missed (e.g. an overlay the scan does not contain) are appended.
/// </summary>
public static class SparseTextMerger
{
    public static string Merge(string ocrText, string digitalText)
    {
        digitalText = (digitalText ?? string.Empty).Trim();
        if (digitalText.Length == 0) return ocrText ?? string.Empty;

        ocrText = (ocrText ?? string.Empty).Trim();
        if (ocrText.Length == 0) return digitalText;

        string ocrNorm = Normalize(ocrText);
        string[] ocrTokens = ocrNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var missing = new List<string>();
        foreach (var line in digitalText.Split('\n'))
        {
            var fragment = line.Trim();
            if (fragment.Length > 0 && !IsPresent(fragment, ocrNorm, ocrTokens))
                missing.Add(fragment);
        }

        if (missing.Count == 0) return ocrText;
        return ocrText + "\n\n" + string.Join("\n", missing);
    }

    private static bool IsPresent(string fragment, string ocrNorm, string[] ocrTokens)
    {
        string fragNorm = Normalize(fragment);
        if (fragNorm.Length == 0) return true;
        if (ocrNorm.Contains(fragNorm, StringComparison.Ordinal)) return true;

        string[] fragTokens = fragNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fragTokens.Length == 1)
        {
            // Single token (e.g. a page number): tolerate small OCR misreads like "17" -> "l7".
            string token = fragTokens[0];
            int tolerance = token.Length <= 3 ? 1 : token.Length <= 8 ? 2 : 3;
            return ocrTokens.Any(t =>
                Math.Abs(t.Length - token.Length) <= tolerance &&
                Levenshtein(t, token, tolerance) <= tolerance);
        }

        // Multi-word fragment: consider it present when most of its words were recognized.
        int hits = fragTokens.Count(ft => ocrTokens.Contains(ft, StringComparer.Ordinal));
        return hits >= (int)Math.Ceiling(fragTokens.Length * 0.6);
    }

    /// <summary>Lowercases and keeps only letters/digits, collapsing everything else to single spaces.</summary>
    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool pendingSpace = false;
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                pendingSpace = false;
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                pendingSpace = true;
            }
        }
        return sb.ToString();
    }

    /// <summary>Levenshtein distance with an early-out once <paramref name="max"/> is exceeded.</summary>
    private static int Levenshtein(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return max + 1;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            int rowMin = curr[0];
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                rowMin = Math.Min(rowMin, curr[j]);
            }
            if (rowMin > max) return max + 1;
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}
