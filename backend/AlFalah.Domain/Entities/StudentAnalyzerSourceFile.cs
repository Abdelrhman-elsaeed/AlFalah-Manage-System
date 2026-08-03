using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>The original school-scoped source file uploaded for student analysis.</summary>
public sealed class StudentAnalyzerSourceFile
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public StudentAnalyzerFileKind FileKind { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ApplicationUser UploadedByUser { get; set; } = null!;
    public ApplicationUser? DeletedByUser { get; set; }
    public ICollection<StudentAnalyzerReport> Reports { get; set; } = new List<StudentAnalyzerReport>();
}
