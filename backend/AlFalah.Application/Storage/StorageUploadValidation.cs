using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace AlFalah.Application.Storage;

// A bounded disk spool verifies the actual length, signature and hash before sending bytes.
// The payload copy uses a 64 KiB buffer; OOXML inspection also reads ZIP directory metadata.
// Disposal removes the spool. File bytes are never loaded wholesale into application memory.
public sealed class ValidatedStorageUpload : IAsyncDisposable
{
    public const long MaxFileBytes = 250L * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 1024 * 1024;
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".doc"] = "application/msword", [".xls"] = "application/vnd.ms-excel",
        [".ppt"] = "application/vnd.ms-powerpoint", [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp",
        [".heic"] = "image/heic", [".mp4"] = "video/mp4", [".mov"] = "video/quicktime"
    };
    private ValidatedStorageUpload(FileStream content, string name, string mime, long size, string hash)
        => (Content, FileName, MimeType, Size, SHA256) = (content, name, mime, size, hash);
    public FileStream Content { get; }
    public string FileName { get; }
    public string MimeType { get; }
    public long Size { get; }
    public string SHA256 { get; }
    public static string SafeName(string name)
    {
        name = name?.Trim().Normalize(NormalizationForm.FormC) ?? "";
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." ||
            name.Contains("..", StringComparison.Ordinal) || name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 ||
            name.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
            throw new ArgumentException("اسم الملف غير صالح.");
        return name;
    }
    public static async Task<ValidatedStorageUpload> ReadAsync(Stream source, string name, long declaredLength, CancellationToken ct)
    {
        name = SafeName(name);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (!MimeTypes.TryGetValue(ext, out var mime)) throw new ArgumentException("نوع الملف غير مسموح.");
        if (declaredLength <= 0 || declaredLength > MaxFileBytes) throw new ArgumentException("حجم الملف يجب أن يكون بين بايت واحد و250 MiB.");
        var path = Path.Combine(Path.GetTempPath(), "alfalah-upload-" + Guid.NewGuid().ToString("N"));
        var spool = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];
            long size = 0;
            int count;
            while ((count = await source.ReadAsync(buffer, ct)) != 0)
            {
                size += count;
                if (size > MaxFileBytes || size > declaredLength) throw new ArgumentException("حجم المحتوى يتجاوز الحد المسموح أو الحجم المعلن.");
                hash.AppendData(buffer, 0, count);
                await spool.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            if (size != declaredLength) throw new ArgumentException("لم يكتمل إرسال الملف. أعد المحاولة بنفس معرّف الرفع.");
            spool.Position = 0;
            var header = new byte[Math.Min(size, 64)];
            await spool.ReadExactlyAsync(header, ct);
            if (!SignatureMatches(ext, header)) throw new ArgumentException("محتوى الملف لا يطابق نوعه المسموح.");
            if (ext is ".docx" or ".xlsx" or ".pptx")
            {
                spool.Position = 0;
                try
                {
                    using var zip = new ZipArchive(spool, ZipArchiveMode.Read, true);
                    var required = ext switch { ".docx" => "word/document.xml", ".xlsx" => "xl/workbook.xml", _ => "ppt/presentation.xml" };
                    if (zip.GetEntry("[Content_Types].xml") is null || zip.GetEntry(required) is null ||
                        zip.Entries.Any(e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                        throw new ArgumentException("ملف Office غير صالح أو يحتوي وحدات ماكرو غير مسموحة.");
                }
                catch (InvalidDataException) { throw new ArgumentException("ملف Office غير صالح."); }
            }
            spool.Position = 0;
            return new(spool, name, mime, size, Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch { await spool.DisposeAsync(); throw; }
    }
    public static bool CanPreview(string? mime) => mime is "application/pdf" or "image/jpeg" or "image/png" or "image/webp" or "video/mp4";
    private static bool SignatureMatches(string ext, byte[] h)
    {
        bool Starts(byte[] magic) => h.AsSpan().StartsWith(magic);
        bool TextAt(int offset, string value) => h.Length >= offset + value.Length && Encoding.ASCII.GetString(h, offset, value.Length) == value;
        return ext switch
        {
            ".pdf" => Starts("%PDF-"u8.ToArray()),
            ".jpg" or ".jpeg" => Starts([0xff, 0xd8, 0xff]),
            ".png" => Starts([137, 80, 78, 71, 13, 10, 26, 10]),
            ".webp" => TextAt(0, "RIFF") && TextAt(8, "WEBP"),
            ".doc" or ".xls" or ".ppt" => Starts([0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1]),
            ".docx" or ".xlsx" or ".pptx" => Starts([0x50, 0x4b, 0x03, 0x04]),
            ".heic" => TextAt(4, "ftyp") && (TextAt(8, "heic") || TextAt(8, "heix") || TextAt(8, "hevc") || TextAt(8, "hevx")),
            ".mp4" => TextAt(4, "ftyp") && (TextAt(8, "isom") || TextAt(8, "iso2") || TextAt(8, "mp41") || TextAt(8, "mp42") || TextAt(8, "avc1")),
            ".mov" => (TextAt(4, "ftyp") && TextAt(8, "qt  ")) || TextAt(4, "moov") || TextAt(4, "mdat"),
            _ => false
        };
    }
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
