using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Automations;

public sealed record AutomationRuleDto(int Id, string Code, StudentTermMetricCode MetricCode, int Threshold, int EffectiveSettingsVersion, bool IsEnabled);
public sealed record AutomationTriggerDto(long Id, string RuleCode, int StudentId, int EligibleCount, int ThresholdSnapshot, AutomationTriggerValidity Validity, DateTimeOffset TriggeredAt);
public sealed record AutomationFailureDto(long Id, string OperationType, string ErrorCode, int Attempts, DateTimeOffset LastAttemptAt, DateTimeOffset? NextRetryAt);
public sealed record RetryAutomationFailureRequestDto(string Reason);

public sealed record GetAutomationRulesQuery : IRequest<ApiResponse<IReadOnlyList<AutomationRuleDto>>>;
public sealed record GetAutomationTriggersQuery(StudentAffairsPageQuery Query) : IRequest<ApiResponse<PagedResult<AutomationTriggerDto>>>;
public sealed record GetAutomationFailuresQuery(StudentAffairsPageQuery Query) : IRequest<ApiResponse<PagedResult<AutomationFailureDto>>>;
public sealed record RetryAutomationFailureCommand(long FailureId, RetryAutomationFailureRequestDto Request) : IRequest<ApiResponse<OperationAcceptedDto>>;
