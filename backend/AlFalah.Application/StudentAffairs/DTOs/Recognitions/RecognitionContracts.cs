using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Recognitions;

public sealed class RecognitionListQuery : StudentAffairsPageQuery
{
    public DateOnly? WeekOf { get; set; }
    public int? Month { get; set; }
    public int? AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public string? RecognitionType { get; set; }
    public int? InstructorProfileId { get; set; }
    public int? StudentId { get; set; }
}

public sealed record CreateRecognitionRequestDto(int StudentId, string RecognitionType, string Title, string Description, DateTimeOffset? RecognizedAt);
public sealed record CorrectRecognitionRequestDto(string RecognitionType, string Title, string Description, DateTimeOffset RecognizedAt, string CorrectionReason, string RowVersion);

public sealed record RecognitionDto(
    int Id,
    StudentSummaryDto Student,
    string RecognitionType,
    string Title,
    string Description,
    DateTimeOffset RecognizedAt,
    ActorSummaryDto Reporter,
    NotificationDeliveryDto? GuardianNotification,
    string RowVersion);

public sealed record RecognitionStatisticBucketDto(string Code, string Label, int Count);
public sealed record RecognitionStatisticsDto(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int TotalStudentsRecognized,
    int TotalRecognitions,
    IReadOnlyList<RecognitionStatisticBucketDto> ByCategory,
    IReadOnlyList<RecognitionStatisticBucketDto> ByClass,
    decimal? ChangeFromPreviousPeriodPercent,
    DateTimeOffset GeneratedAt);

public sealed record CreateRecognitionCommand(CreateRecognitionRequestDto Request) : IRequest<ApiResponse<RecognitionDto>>;
public sealed record GetRecognitionsQuery(RecognitionListQuery Query) : IRequest<ApiResponse<PagedResult<RecognitionDto>>>;
public sealed record GetRecognitionStatisticsQuery(RecognitionListQuery Query) : IRequest<ApiResponse<RecognitionStatisticsDto>>;
public sealed record GetRecognitionByIdQuery(int RecognitionId) : IRequest<ApiResponse<RecognitionDto>>;
public sealed record CorrectRecognitionCommand(int RecognitionId, CorrectRecognitionRequestDto Request) : IRequest<ApiResponse<RecognitionDto>>;
