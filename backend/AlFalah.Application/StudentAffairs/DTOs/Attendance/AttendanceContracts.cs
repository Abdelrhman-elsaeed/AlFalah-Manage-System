using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Attendance;

public sealed class StudentAttendanceRecordsQuery : StudentAffairsPageQuery
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int? ClassroomId { get; set; }
    public int? StudentId { get; set; }
    public StudentAttendanceStatus? Status { get; set; }
    public AbsenceExcuseStatus? ExcuseStatus { get; set; }
    public string? Severity { get; set; }
}

public sealed record SubmitAbsentRosterRequestDto(
    DateOnly Date,
    int ClassroomId,
    IReadOnlyList<int> AbsentStudentIds,
    string RosterRevision);

public sealed record CorrectStudentAttendanceRequestDto(
    StudentAttendanceStatus Status,
    string CorrectionReason,
    string RowVersion);

public sealed record SubmitAbsenceExcuseRequestDto(AbsenceExcuseType ExcuseType, string? Notes);
public sealed record ReviewAbsenceExcuseRequestDto(string? ReviewNote, string RowVersion);
public sealed record RejectAbsenceExcuseRequestDto(string RejectionReason, string RowVersion);

public sealed record StudentAttendanceSheetRowDto(
    int? AttendanceId,
    StudentSummaryDto Student,
    StudentAttendanceStatus Status,
    AbsenceExcuseStatus? ExcuseStatus,
    ActorSummaryDto? RecordedBy,
    DateTimeOffset? RecordedAt,
    MetricBadgeDto PenaltyEligibleAbsenceBadge,
    string? RowVersion);

public sealed record StudentAttendanceSheetDto(
    DateOnly Date,
    ClassroomSummaryDto Classroom,
    string RosterRevision,
    bool IsSaved,
    IReadOnlyList<StudentAttendanceSheetRowDto> Rows);

public sealed record StudentAttendanceRecordDto(
    int Id,
    StudentSummaryDto Student,
    DateOnly Date,
    StudentAttendanceStatus Status,
    AbsenceExcuseStatus? ExcuseStatus,
    ActorSummaryDto RecordedBy,
    DateTimeOffset RecordedAt,
    string RowVersion);

public sealed record StudentAttendanceHistoryDto(
    StudentSummaryDto Student,
    AcademicTermSummaryDto Term,
    IReadOnlyList<StudentAttendanceRecordDto> Records,
    MetricBadgeDto AbsenceMetric);

public sealed record AbsenceExcuseDto(
    int Id,
    AbsenceExcuseType ExcuseType,
    AbsenceExcuseStatus Status,
    GuardianSummaryDto Guardian,
    DateTimeOffset SubmittedAt,
    ActorSummaryDto? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewReason,
    IReadOnlyList<AttachmentDto> Attachments,
    string RowVersion);

public sealed record GetStudentAttendanceSheetQuery(DateOnly Date, int ClassroomId) : IRequest<ApiResponse<StudentAttendanceSheetDto>>;
public sealed record SubmitAbsentRosterCommand(SubmitAbsentRosterRequestDto Request, string IdempotencyKey) : IRequest<ApiResponse<StudentAttendanceSheetDto>>;
public sealed record CorrectStudentAttendanceCommand(int AttendanceId, CorrectStudentAttendanceRequestDto Request) : IRequest<ApiResponse<StudentAttendanceRecordDto>>;
public sealed record GetStudentAttendanceRecordsQuery(StudentAttendanceRecordsQuery Query) : IRequest<ApiResponse<PagedResult<StudentAttendanceRecordDto>>>;
public sealed record GetStudentAttendanceHistoryQuery(int StudentId, int? AcademicTermId) : IRequest<ApiResponse<StudentAttendanceHistoryDto>>;
public sealed record SubmitAbsenceExcuseCommand(
    int AttendanceId,
    SubmitAbsenceExcuseRequestDto Request,
    string IdempotencyKey,
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long SizeBytes) : IRequest<ApiResponse<AbsenceExcuseDto>>;
public sealed record GetAbsenceExcusesQuery(int AttendanceId) : IRequest<ApiResponse<IReadOnlyList<AbsenceExcuseDto>>>;
public sealed record DownloadAbsenceExcuseAttachmentQuery(int ExcuseId, int AttachmentId) : IRequest<AuthorizedFileDto>;
public sealed record AcceptAbsenceExcuseCommand(int ExcuseId, ReviewAbsenceExcuseRequestDto Request) : IRequest<ApiResponse<AbsenceExcuseDto>>;
public sealed record RejectAbsenceExcuseCommand(int ExcuseId, RejectAbsenceExcuseRequestDto Request) : IRequest<ApiResponse<AbsenceExcuseDto>>;
