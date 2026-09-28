using AlFalah.Application.StudentAffairs.DTOs.Automations;
using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Recognitions;

namespace AlFalah.Application.StudentAffairs;

/// <summary>
/// W1 containment for public contracts whose workflows belong to later workstreams.
/// Their controllers return an explicit 501 response and must never dispatch them to MediatR.
/// </summary>
public static class DeferredStudentAffairsRequests
{
    public static IReadOnlyDictionary<Type, string> Workstreams { get; } = new Dictionary<Type, string>
    {
        [typeof(GetAcademicConcernsQuery)] = "W6 Academic Concern Review",
        [typeof(GetAcademicConcernByIdQuery)] = "W6 Academic Concern Review",
        [typeof(DecideAcademicConcernDispatchCommand)] = "W6 Academic Concern Review",
        [typeof(CorrectAcademicConcernCommand)] = "W6 Academic Concern Review",
        [typeof(GetBehaviorIncidentsQuery)] = "W6 Conduct Review",
        [typeof(GetBehaviorIncidentByIdQuery)] = "W6 Conduct Review",
        [typeof(ClassifyBehaviorIncidentCommand)] = "W6 Conduct Review",
        [typeof(DecideBehaviorDispatchCommand)] = "W6 Conduct Review",
        [typeof(ReferBehaviorIncidentCommand)] = "W6 Conduct Review",
        [typeof(CorrectBehaviorIncidentCommand)] = "W6 Conduct Review",
        [typeof(GetMorningDelaysQuery)] = "W6 Delay Review",
        [typeof(GetMorningDelayByIdQuery)] = "W6 Delay Review",
        [typeof(ProvideMorningDelayReasonCommand)] = "W6 Delay Review",
        [typeof(CorrectMorningDelayCommand)] = "W6 Delay Review",
        [typeof(GetSessionDelaysQuery)] = "W6 Delay Review",
        [typeof(GetSessionDelayByIdQuery)] = "W6 Delay Review",
        [typeof(CorrectSessionDelayCommand)] = "W6 Delay Review",
        [typeof(GetRecognitionsQuery)] = "W6 Recognition Review",
        [typeof(GetRecognitionStatisticsQuery)] = "W6 Recognition Review",
        [typeof(GetRecognitionByIdQuery)] = "W6 Recognition Review",
        [typeof(CorrectRecognitionCommand)] = "W6 Recognition Review",

        [typeof(GetAutomationRulesQuery)] = "W7 Automations and Notifications",
        [typeof(GetAutomationTriggersQuery)] = "W7 Automations and Notifications",
        [typeof(GetAutomationFailuresQuery)] = "W7 Automations and Notifications",
        [typeof(RetryAutomationFailureCommand)] = "W7 Automations and Notifications"
    };
}
