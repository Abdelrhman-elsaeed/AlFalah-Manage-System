using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AlFalah.Application.Storage;

// Data only: paths and source status are provenance, never executed or interpreted as approval.
public static class PrototypeImportParser
{
    public const int MaxBytes = 16 * 1024 * 1024;
    public const int MaxRows = 10000;
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static async Task<(string Hash, IReadOnlyList<ImportSourceRow> Rows)> ReadAsync(Stream stream, string name, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var block = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) throw new ArgumentException("حد المصدر 16 MiB.");
            await buffer.WriteAsync(block.AsMemory(0, read), ct);
        }
        var bytes = buffer.ToArray();
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw new ArgumentException("المصدر يجب أن يكون UTF-8."); }
        IReadOnlyList<ImportSourceRow> rows;
        try
        {
            rows = Path.GetExtension(name).ToLowerInvariant() switch
            {
                ".json" => Json(text), ".csv" => Csv(text),
                _ => throw new ArgumentException("المصدر يجب أن يكون JSON أو CSV.")
            };
        }
        catch (JsonException) { throw new ArgumentException("JSON غير صالح أو يتجاوز عمق 16."); }
        if (rows.Count is 0 or > MaxRows) throw new ArgumentException("المصدر يقبل من 1 إلى 10000 صف.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Key) || row.Key.Length > 128 || !keys.Add(row.Key)) throw new ArgumentException("مفتاح الصف مفقود أو مكرر أو يتجاوز 128 حرفًا.");
            if (string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 512 || row.ReferencePath?.Length > 2048 ||
                row.ResponsibleName?.Length > 512 || row.SourceStatus?.Length > 128 || row.Extension?.Length > 16 ||
                row.DomainCode?.Length > 16 || row.StandardCode?.Length > 16 || row.RequirementCode?.Length > 128 || row.Size < 0)
                throw new ArgumentException("قيمة صف غير صالحة أو أطول من الحد المسموح.");
        }
        return (Convert.ToHexString(SHA256.HashData(bytes)), rows);
    }
    private static IReadOnlyList<ImportSourceRow> Json(string text)
    {
        using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
        var data = doc.RootElement;
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("files", out var files)) data = files;
        else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("rows", out var rows)) data = rows;
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > MaxRows) throw new ArgumentException("المصدر يجب أن يحتوي مصفوفة صفوف.");
        if (data.EnumerateArray().Any(r => r.ValueKind != JsonValueKind.Object || r.EnumerateObject().Count() > 32)) throw new ArgumentException("كل صف يجب أن يكون كائنًا بحد أقصى 32 حقلًا.");
        return data.EnumerateArray().Select((r, i) => From(r.EnumerateObject().ToDictionary(p => p.Name, p =>
            p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText()), i)).ToArray();
    }
    private static ImportSourceRow From(IReadOnlyDictionary<string, string> row, int ordinal)
    {
        string? Value(params string[] keys) => keys.Select(k => row.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && v != "null");
        var size = Value("size", "sizeBytes", "size_bytes", "size_kb");
        long? length = null;
        if (size != null)
        {
            if (!decimal.TryParse(size, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || number < 0 || number > long.MaxValue / 1024m)
                throw new ArgumentException("حجم مرجع المصدر غير صالح.");
            length = (long)(number * (row.ContainsKey("size_kb") && !row.ContainsKey("size") ? 1024 : 1));
        }
        var standard = Value("standardCode", "standard_code", "standard", "domainKey");
        if (standard != null) standard = standard.Split(' ')[0];
        return new(Value("key", "id", "sourceKey") ?? $"row-{ordinal + 1}", Value("name", "fileName", "file_name", "filename", "doc") ?? "",
            Value("domainCode", "domain_code"), standard, Value("requirementCode", "requirement_code"),
            Value("responsibleName", "responsible", "role"), Value("referencePath", "relPath", "rel_path", "path", "targetRelPath"),
            length, Value("extension", "ext"), Value("sourceStatus", "status", "completed"));
    }
    private static IReadOnlyList<ImportSourceRow> Csv(string text)
    {
        var records = new List<string[]>(); var cells = new List<string>(); var cell = new StringBuilder(); bool quote = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"') { if (quote && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else quote = !quote; }
            else if (!quote && c is ',' or '\n')
            {
                cells.Add(cell.ToString().TrimEnd('\r')); cell.Clear();
                if (c == '\n') { records.Add(cells.ToArray()); cells.Clear(); }
            }
            else cell.Append(c);
            if (cell.Length > 4096 || records.Count > MaxRows + 1 || cells.Count > 32) throw new ArgumentException("CSV يتجاوز حدود القيم أو الأعمدة أو الصفوف.");
        }
        if (quote) throw new ArgumentException("CSV يحتوي اقتباسًا غير مغلق.");
        if (cell.Length > 0 || cells.Count > 0) { cells.Add(cell.ToString().TrimEnd('\r')); records.Add(cells.ToArray()); }
        if (records.Count < 2 || records[0].Distinct().Count() != records[0].Length) throw new ArgumentException("عناوين CSV غير صالحة.");
        return records.Skip(1).Select((r, i) => r.Length == records[0].Length ? From(records[0].Zip(r).ToDictionary(x => x.First, x => x.Second), i)
            : throw new ArgumentException("عدد أعمدة CSV غير متطابق.")).ToArray();
    }
}
