using System.Text;
using SmartLawyer.Core.Arabic;
using UglyToad.PdfPig;

namespace SmartLawyer.Infrastructure.CaseAnalysis;

public sealed class UnsupportedDocumentException(string message) : Exception(message);

/// <summary>استخراج النص من المستندات المرفوعة (PDF فيه نص، أو ملف نصي). الملف الأصلي مابيتخزنش.</summary>
public static class DocumentTextReader
{
    public const int MaxBytes = 10 * 1024 * 1024;

    public static string Read(string fileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw new UnsupportedDocumentException("الملف فاضي.");
        }
        if (content.Length > MaxBytes)
        {
            throw new UnsupportedDocumentException("الملف أكبر من 10 ميجا.");
        }

        string raw;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".pdf")
        {
            try
            {
                using var pdf = PdfDocument.Open(content);
                raw = string.Join("\n", pdf.GetPages().Select(p => p.Text));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw new UnsupportedDocumentException("مقدرناش نفتح ملف الـ PDF ده.");
            }
        }
        else if (ext is ".txt" or ".text")
        {
            raw = Encoding.UTF8.GetString(content);
        }
        else
        {
            throw new UnsupportedDocumentException("الأنواع المدعومة دلوقتي: PDF و TXT.");
        }

        var text = ArabicPdfTextCleaner.Clean(raw).Trim();
        if (text.Length < 20)
        {
            // غالباً PDF متصوّر (scan) من غير نص.
            throw new UnsupportedDocumentException("مالقيناش نص في الملف. لو ده PDF متصوّر (scan)، الدعم بتاعه لسه مش موجود.");
        }
        return text;
    }
}
