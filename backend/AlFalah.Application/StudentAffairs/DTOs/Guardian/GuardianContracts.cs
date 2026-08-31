using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Guardian;

public sealed record GuardianStudentDto(StudentSummaryDto Student, bool CanSubmitExcuses, bool CanRequestGatePass, bool ReceivesNotifications);
public sealed record GuardianStudentSummaryDto(StudentContextDto Context, int PendingSummons, int ActiveGatePasses, int RecentRecognitions);
public sealed record GuardianNotificationDto(int Id, int StudentId, string Type, string Title, string Body, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public sealed record GetGuardianStudentsQuery : IRequest<ApiResponse<IReadOnlyList<GuardianStudentDto>>>;
public sealed record GetGuardianStudentSummaryQuery(int StudentId) : IRequest<ApiResponse<GuardianStudentSummaryDto>>;
public sealed record GetGuardianStudentNotificationsQuery(int StudentId, StudentAffairsPageQuery Query) : IRequest<ApiResponse<PagedResult<GuardianNotificationDto>>>;
