using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Classrooms;

public sealed class ClassroomListQuery : StudentAffairsPageQuery
{
    public int? AcademicYearId { get; set; }
    public int? AcademicTermId { get; set; }
}

public sealed record CreateClassroomRequestDto(
    int AcademicYearId,
    SchoolStage Stage,
    byte GradeLevel,
    string Section,
    string ClassLabel);

public sealed record UpdateClassroomRequestDto(string ClassLabel, string Section, bool IsActive, string RowVersion);
public sealed record ArchiveClassroomRequestDto(string Reason, string RowVersion);

public sealed record ClassroomDto(
    int Id,
    string Label,
    SchoolStage Stage,
    byte GradeLevel,
    string Section,
    int AcademicYearId,
    string AcademicYearLabel,
    bool IsActive,
    int ActiveEnrollmentCount,
    string RowVersion);

public sealed record GetClassroomsQuery(ClassroomListQuery Query) : IRequest<ApiResponse<PagedResult<ClassroomDto>>>;
public sealed record CreateClassroomCommand(CreateClassroomRequestDto Request) : IRequest<ApiResponse<ClassroomDto>>;
public sealed record UpdateClassroomCommand(int ClassroomId, UpdateClassroomRequestDto Request) : IRequest<ApiResponse<ClassroomDto>>;
public sealed record ArchiveClassroomCommand(int ClassroomId, ArchiveClassroomRequestDto Request) : IRequest<ApiResponse<bool>>;
public sealed record GetClassroomStudentsQuery(int ClassroomId, int? AcademicTermId) : IRequest<ApiResponse<IReadOnlyList<StudentSummaryDto>>>;
