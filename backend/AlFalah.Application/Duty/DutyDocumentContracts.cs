using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Storage;

namespace AlFalah.Application.Duty;

public sealed class DutyArchiveOptions
{
    public const string Section = "SchoolDutyArchive";
    public bool ProcessingEnabled { get; set; }
}
public sealed record DutyPdf(byte[] Bytes, string FileName, string Hash, string RendererVersion);
public sealed record DutyDocumentState(int DocumentId, string Kind, int AssignmentRevision, DateTimeOffset SignedAt,
    int? PreviousDocumentId, string? CorrectionReason, string ArtifactState, string ArchiveState, bool CanRetry,
    int Attempts, DateTimeOffset? NextAttemptAt, DateTimeOffset? VerifiedAt, string? ErrorCode, string AuthorName, string DutyDate, bool IsCurrent);
public sealed record DutyArchiveReadiness(bool Enabled, bool ProcessingEnabled, string ConnectionState);
public sealed record DutyDocumentCollection(DutyArchiveReadiness Readiness, IReadOnlyList<DutyDocumentState> Documents);
public sealed record DutyRosterSnapshot(int SchoolId, int PlanId, int Revision, DutyExecutionHeader Header,
    DateOnly From, DateOnly To, IReadOnlyList<int> Weekdays, IReadOnlyList<DutyRosterRow> Rows, string TimeZoneId, DutyLogoSnapshot? Logo = null);
public sealed record DutyRosterRow(DateOnly Date, string Teacher, string Notes, bool Closed);
public sealed record DutyArchiveScope(int YearId, int TermId);
public sealed record DutyArchiveQuery(int? TermId=null,string? TeacherId=null,DateOnly? From=null,DateOnly? To=null,int Page=1);
public sealed record DutyArchiveGroup(int AssignmentId,string AuthorId,int Revision,DateOnly Date,string Name,bool IsCurrent,int DocumentCount);
public sealed record DutyArchiveSlice(int Total,IReadOnlyList<DutyArchiveGroup> Groups,IReadOnlyList<DutySignedDocument> Documents,IReadOnlyList<DutyArchiveIntent> Intents,IReadOnlyList<DutyPdfArtifact> Artifacts);
public sealed record DutyArchiveRow(int AssignmentId,string TeacherId,string Teacher,DateOnly Date,int Revision,bool IsCurrent,int RevisionCount,DutyDocumentState? Letter,DutyDocumentState? Report);
public sealed record DutyArchivePage(int Page,int PageSize,int Total,DutyArchiveReadiness Readiness,IReadOnlyList<DutyArchiveRow> Rows);
public static class DutyDocumentIntegrity
{
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    public static JsonElement Validate(DutySignedDocument doc)
    {
        try { return ValidateEvidence(doc); }
        catch (DutyException) { throw; }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or NullReferenceException) { throw Error(); }
    }
    private static JsonElement ValidateEvidence(DutySignedDocument doc)
    {
        if (Hash(doc.SnapshotJson) != doc.SnapshotHash || Hash(doc.SignatureBytes) != doc.SignatureHash || doc.SignatureBytes.Length < 33)
            throw Error();
        var s = JsonSerializer.Deserialize<JsonElement>(doc.SnapshotJson);
        if (s.GetProperty("AuthorUserId").GetString() != doc.AuthorUserId || s.GetProperty("EffectiveTeacherId").GetString() != doc.AuthorUserId
            || s.GetProperty("AssignmentRevision").GetInt32() != doc.AssignmentRevision || s.GetProperty("TemplateVersion").GetString() != doc.TemplateVersion
            || s.GetProperty("SignedAt").GetDateTimeOffset() != doc.SignedAt || doc.Kind is not ("Letter" or "Report")) throw Error();
        var original = s.GetProperty("OriginalTeacherId").GetString() == doc.AuthorUserId;
        if (s.GetProperty("SignaturePosition").GetString() != (original ? "OriginalTeacher" : "SubstituteTeacher")) throw Error();
        if (s.GetProperty("Header").GetProperty("IsSubstitute").GetBoolean() == original) throw Error();
        var logo = s.GetProperty("Logo");
        if (logo.GetProperty("State").GetString() == "Frozen")
        {
            var bytes = Convert.FromBase64String(logo.GetProperty("Data").GetString()!.Split(',').Last());
            if (Hash(bytes) != logo.GetProperty("Hash").GetString()) throw Error();
        }
        return s;
    }
    public static void Validate(DutyPdfArtifact artifact)
    {
        if (artifact.Bytes.Length < 100 || !artifact.Bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8) || Hash(artifact.Bytes) != artifact.Hash
            || Hash(artifact.ProvenanceJson) != artifact.ProvenanceHash) throw Error();
    }
    public static DutyException Error() => new("DutyArtifactUnavailable", "النسخة المحفوظة غير متاحة حاليًا. راجع المدير. / The retained document is currently unavailable. Contact your manager.", 409);
    public static string SafeName(string value) => new(value.Where(c => !char.IsControl(c) && !"/\\:*?\"<>|".Contains(c)).Take(140).ToArray());
}
public interface IDutyPdfRenderer
{
    string Version { get; }
    string RosterVersion => Version;
    DutyPdf Signed(DutySignedDocument document);
    DutyPdf Roster(DutyRosterSnapshot snapshot);
}
public interface IDutyDocumentRepository
{
    Task<DutyArchiveSlice> ArchiveAsync(int school,DutyArchiveQuery query,DateOnly today,CancellationToken ct);
    Task<DutyArchiveReadiness> ReadinessAsync(int school, CancellationToken ct);
    Task SetEnabledAsync(int school, bool enabled, string actor, CancellationToken ct);
    Task<DutyPdfArtifact?> ArtifactAsync(int school, int document, CancellationToken ct);
    Task<DutyPdfArtifact?> RosterArtifactAsync(int school, int plan, int revision, CancellationToken ct, string? rendererVersion = null);
    Task AddArtifactAsync(DutyPdfArtifact artifact, CancellationToken ct);
    Task<DutyArchiveIntent?> IntentAsync(int school, int document, CancellationToken ct);
    Task<List<DutyArchiveIntent>> IntentsAsync(int school, int assignment, CancellationToken ct);
    Task<List<(int School, int Document)>> DueAsync(DateTimeOffset now, CancellationToken ct);
    Task<List<(int School, int Document)>> ReconcileDueAsync(CancellationToken ct);
    Task<bool> ExclusiveAsync(string resource, Func<CancellationToken, Task> action, CancellationToken ct);
    Task<DutyArchiveIntent?> ClaimAsync(int school, int document, Guid token, DateTimeOffset now, CancellationToken ct);
    Task RenewAsync(int id, Guid token, DateTimeOffset now, CancellationToken ct);
    Task SaveAsync(DutyArchiveIntent intent, CancellationToken ct);
    Task FailAsync(int school, int document, Guid token, string code, DateTimeOffset now, CancellationToken ct);
    Task CompleteAsync(DutyArchiveIntent intent, DutyPdfArtifact artifact, bool real, CancellationToken ct);
    Task RetryAsync(int school, int document, string actor, CancellationToken ct);
    Task<DutyArchiveScope> ScopeAsync(int school, int assignment, CancellationToken ct);
    Task<StorageFolder?> FolderAsync(int school, string identity, CancellationToken ct);
    Task<StorageFolder?> FolderByIdAsync(int school, int id, CancellationToken ct);
    Task SaveFolderAsync(StorageFolder folder, CancellationToken ct);
    Task<string> TeacherFolderNameAsync(int school, string user, string name, CancellationToken ct);
    Task MarkAvailabilityAsync(DutyArchiveIntent intent, string? error, DateTimeOffset at, CancellationToken ct);
}
public interface IDutyDocumentService
{
    Task<DutyArchivePage> ArchiveAsync(DutyArchiveQuery query,CancellationToken ct);
    Task<DutyArchiveReadiness> ReadinessAsync(CancellationToken ct);
    Task<DutyDocumentCollection> ListAsync(int assignment, CancellationToken ct);
    Task<DutyPdf> ContentAsync(int document, bool archived, CancellationToken ct);
    Task<DutyPdf> RosterAsync(int term, int revision, CancellationToken ct);
    Task RetryAsync(int document, CancellationToken ct);
    Task<DutyArchiveReadiness> EnableAsync(bool enabled, CancellationToken ct);
}
