using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record TimetableSetupAcademicYearDto(int Id, string Code, string NameAr, bool IsActive);

public sealed record CreateTimetableAcademicYearRequest(
    string Code,
    string NameAr,
    DateOnly StartsOn,
    DateOnly EndsOn,
    DateOnly FirstSemesterStartsOn,
    DateOnly FirstSemesterEndsOn,
    DateOnly SecondSemesterStartsOn,
    DateOnly SecondSemesterEndsOn,
    TimetableSemester ActiveSemester);

public sealed record TimetableSetupProfileDto(
    int Id,
    int SchoolId,
    int AcademicYearId,
    string AcademicYearName,
    int Semester,
    string SemesterLabelAr,
    string Name,
    int? BellScheduleTemplateId,
    TimetableSetupStatus Status,
    string StatusLabelAr,
    int Revision,
    DateTimeOffset UpdatedAt);

public sealed record TimetableReadinessCountsDto(
    int ActiveClassrooms,
    int ActiveStudents,
    int ClassroomsMissingLocation,
    int ActiveTeachers,
    int AvailableSubjects);

public sealed record TimetableSetupStepDto(
    string Key,
    string TitleAr,
    string Status,
    string DescriptionAr,
    string Route);

public sealed record TimetableSettingsOverviewDto(
    int SchoolId,
    string SchoolName,
    IReadOnlyList<TimetableSetupAcademicYearDto> AcademicYears,
    IReadOnlyList<TimetableSetupProfileDto> Profiles,
    TimetableSetupProfileDto? SelectedProfile,
    int SelectedAcademicYearId,
    int SelectedSemester,
    string SelectedSemesterLabelAr,
    bool CanManage,
    int CompletionPercent,
    bool HardPrerequisitesValid,
    TimetableReadinessCountsDto Counts,
    IReadOnlyList<TimetableSetupStepDto> Steps,
    IReadOnlyList<string> Warnings,
    BellScheduleDto? BellSchedule = null);

public sealed record CreateTimetableSetupProfileRequest(
    int AcademicYearId,
    TimetableSemester Semester,
    string Name);

public sealed record UpdateTimetableSetupProfileRequest(
    string Name,
    int Revision);

public sealed record GetTimetableSettingsQuery(
    int? AcademicYearId,
    TimetableSemester? Semester,
    int? ProfileId) : IRequest<ApiResponse<TimetableSettingsOverviewDto>>;

public sealed record CreateTimetableSetupProfileCommand(
    CreateTimetableSetupProfileRequest Request) : IRequest<ApiResponse<TimetableSetupProfileDto>>;

public sealed record CreateTimetableAcademicYearCommand(
    CreateTimetableAcademicYearRequest Request) : IRequest<ApiResponse<TimetableSetupAcademicYearDto>>;

public sealed record UpdateTimetableSetupProfileCommand(
    int ProfileId,
    UpdateTimetableSetupProfileRequest Request) : IRequest<ApiResponse<TimetableSetupProfileDto>>;
