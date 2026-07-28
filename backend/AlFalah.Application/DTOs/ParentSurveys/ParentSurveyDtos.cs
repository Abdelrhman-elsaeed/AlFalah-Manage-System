using AlFalah.Domain.Enums;

namespace AlFalah.Application.DTOs.ParentSurveys;

public record ParentSurveyItemWriteDto(int? Id, string Text);

public record SaveParentSurveyRequestDto(
    int? SchoolId,
    string Title,
    string? Description,
    bool IsTemplate,
    int? SourceTemplateId,
    IReadOnlyList<ParentSurveyItemWriteDto> Items);

public record ParentSurveyItemDto(int Id, string Text, int SortOrder);

public record ParentSurveyDto(
    int Id,
    int SchoolId,
    string SchoolName,
    string Title,
    string? Description,
    bool IsTemplate,
    ParentSurveyStatus Status,
    string? PublicToken,
    int SubmissionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ParentSurveyItemDto> Items);

public record PublishParentSurveyDto(string PublicToken, DateTimeOffset PublishedAt);

public record PublicParentSurveyDto(
    string Title,
    string? Description,
    string SchoolName,
    string? SchoolLogoUrl,
    bool IsAcceptingResponses,
    IReadOnlyList<ParentSurveyItemDto> Items);

public record SubmitParentSurveyAnswerDto(int ItemId, ParentSurveyRating Rating, string? WeakReason);

public record SubmitParentSurveyRequestDto(
    string ParentName,
    string MobileNumber,
    IReadOnlyList<SubmitParentSurveyAnswerDto> Answers);

public record ParentSurveySubmissionListItemDto(
    int Id,
    string ParentName,
    string MobileNumber,
    DateTimeOffset SubmittedAt,
    int AutoAdjustedAnswerCount);

public record ParentSurveyAnswerDto(
    int ItemId,
    string ItemText,
    ParentSurveyRating SubmittedRating,
    ParentSurveyRating EffectiveRating,
    string? WeakReason,
    bool WasAutoAdjusted);

public record ParentSurveySubmissionDto(
    int Id,
    int ParentSurveyId,
    string ParentName,
    string MobileNumber,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<ParentSurveyAnswerDto> Answers);
