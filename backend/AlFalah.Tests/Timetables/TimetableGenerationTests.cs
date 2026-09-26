using AlFalah.Application.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class TimetableGenerationTests
{
    private readonly TimetableGenerationEngine _engine = new();

    [Fact]
    public void Generator_places_a_fixed_subject_in_its_exact_slot()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Requirements[0].FixedSlots.Add(new() { Day = 3, Period = 4 });

        var generated = _engine.Generate(context, default);

        generated.Should().ContainSingle(x => x.ClassSubjectRequirementId == 1 &&
            x.Day == TimetableDay.Monday && x.Period == 4);
    }

    [Fact]
    public void Generator_rejects_two_fixed_lessons_that_require_the_same_teacher_at_the_same_time()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Requirements[0].FixedSlots.Add(new() { Day = 3, Period = 4 });
        var second = new ClassSubjectRequirement
        {
            Id = 2, SchoolId = 1, TimetableSetupProfileId = 1, ClassroomId = 2, SubjectId = 1,
            Classroom = context.Classrooms[1], Subject = context.Subjects[0], IndividualPeriodCount = 1,
            FixedSlots = [new() { Day = 3, Period = 4 }]
        };
        var assignment = new TeachingAssignment
        {
            Id = 2, SchoolId = 1, TimetableSetupProfileId = 1, ClassSubjectRequirementId = 2,
            Members = [new() { Id = 2, SchoolId = 1, TimetableSetupProfileId = 1, TeachingAssignmentId = 2,
                TeacherTimetableProfileId = 1, AllocatedPeriodCount = 1 }]
        };
        var expanded = context with
        {
            Requirements = [.. context.Requirements, second],
            Assignments = [.. context.Assignments, assignment]
        };

        var action = () => _engine.Generate(expanded, default);

        action.Should().Throw<ArgumentException>().WithMessage("*تعذر إنشاء جدول*");
    }

    [Fact]
    public void Generator_places_the_same_fixed_subject_for_multiple_classes_at_one_unified_time()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Requirements[0].FixedSlots.Add(new() { Day = 3, Period = 4 });
        var expanded = AddRequirement(context, id: 2, classroomIndex: 1, teacherProfileId: 2,
            fixedDay: 3, fixedPeriod: 4);

        var generated = _engine.Generate(expanded, default);

        generated.Where(x => x.SubjectId == 1 && x.Day == TimetableDay.Monday && x.Period == 4)
            .Should().HaveCount(2)
            .And.OnlyHaveUniqueItems(x => x.ClassroomId);
    }

    [Fact]
    public void Generator_honors_allowed_days_for_every_generated_occurrence()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Requirements[0].IndividualPeriodCount = 3;
        context.Requirements[0].AllowedDays.Add(new() { Day = 5 });
        context.Assignments[0].Members.Single().AllocatedPeriodCount = 3;

        var generated = _engine.Generate(context, default);

        generated.Should().HaveCount(3).And.OnlyContain(x => x.Day == TimetableDay.Wednesday);
    }

    [Fact]
    public void Generator_keeps_a_paired_block_adjacent_without_crossing_a_break()
    {
        var context = TimetableReviewPhase7Tests.Context();
        var requirement = context.Requirements[0];
        requirement.IndividualPeriodCount = 0;
        requirement.PairedBlockCount = 1;
        requirement.FixedSlots.Add(new() { Day = 3, Period = 4 });
        var member = context.Assignments[0].Members.Single();
        member.AllocatedPeriodCount = 2;
        member.AllocatedPairedBlockCount = 1;

        var generated = _engine.Generate(context, default).OrderBy(x => x.Period).ToArray();

        generated.Should().HaveCount(2);
        generated.Should().OnlyContain(x => x.Day == TimetableDay.Monday);
        generated.Select(x => x.Period).Should().Contain(4);
        generated[1].Period.Should().Be(generated[0].Period + 1);
    }

    [Fact]
    public void Generator_places_all_co_teachers_in_the_same_occurrence()
    {
        var context = TimetableReviewPhase7Tests.Context();
        var assignment = context.Assignments[0];
        assignment.Mode = "CoTeaching";
        assignment.Members.Add(new()
        {
            Id = 2, SchoolId = 1, TimetableSetupProfileId = 1, TeachingAssignmentId = 1,
            TeacherTimetableProfileId = 2, AllocatedPeriodCount = 1
        });

        var generated = _engine.Generate(context, default);

        generated.Should().HaveCount(2);
        generated.Select(x => x.InstructorProfileId).Should().BeEquivalentTo([1, 2]);
        generated.Select(x => new { x.Day, x.Period, x.ClassroomId }).Distinct().Should().ContainSingle();
    }

    [Fact]
    public void Generator_respects_split_quota_between_teachers()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Requirements[0].IndividualPeriodCount = 2;
        var assignment = context.Assignments[0];
        assignment.Mode = "SplitQuota";
        assignment.Members.Single().AllocatedPeriodCount = 1;
        assignment.Members.Add(new()
        {
            Id = 2, SchoolId = 1, TimetableSetupProfileId = 1, TeachingAssignmentId = 1,
            TeacherTimetableProfileId = 2, AllocatedPeriodCount = 1
        });

        var generated = _engine.Generate(context, default);

        generated.Should().HaveCount(2);
        generated.GroupBy(x => x.InstructorProfileId).Should().HaveCount(2)
            .And.OnlyContain(group => group.Count() == 1);
    }

    [Fact]
    public void Generator_rejects_a_fixed_slot_when_the_assigned_teacher_is_unavailable()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Requirements[0].FixedSlots.Add(new() { Day = 2, Period = 1 });
        context.Teachers[0].Slots.Add(new()
        {
            Id = 1, TeacherTimetableProfileId = 1, Day = 2, BellPeriodId = 1, IsAvailable = false,
            Period = context.Schedule!.Days.First().Periods.Single(x => x.Sequence == 1)
        });

        var action = () => _engine.Generate(context, default);

        action.Should().Throw<ArgumentException>().WithMessage("*لا يوجد موعد صالح*");
    }

    [Fact]
    public void Generator_prevents_two_classes_from_using_the_same_required_room_at_one_fixed_time()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Requirements[0].FixedSlots.Add(new() { Day = 3, Period = 4 });
        context.Requirements[0].Rooms.Add(RoomRequirement(1, 1));
        var expanded = AddRequirement(context, id: 2, classroomIndex: 1, teacherProfileId: 2,
            fixedDay: 3, fixedPeriod: 4);
        expanded.Requirements[1].Rooms.Add(RoomRequirement(2, 1));

        var action = () => _engine.Generate(expanded, default);

        action.Should().Throw<ArgumentException>().WithMessage("*تعذر إنشاء جدول*");
    }

    [Fact]
    public void Generator_rejects_load_above_the_teacher_weekly_maximum()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Teachers[0].MaximumWeeklyPeriods = 0;

        var action = () => _engine.Generate(context, default);

        action.Should().Throw<ArgumentException>().WithMessage("*يتجاوز الحد الأسبوعي*");
    }

    [Fact]
    public void Generator_rejects_more_fixed_slots_than_the_subject_weekly_load()
    {
        var context = TimetableReviewPhase7Tests.Context();
        context.Requirements[0].FixedSlots.Add(new() { Day = 2, Period = 1 });
        context.Requirements[0].FixedSlots.Add(new() { Day = 3, Period = 1 });

        var action = () => _engine.Generate(context, default);

        action.Should().Throw<ArgumentException>().WithMessage("*تتجاوز النصاب الأسبوعي*");
    }

    [Fact]
    public void Generator_rejects_a_subject_without_a_teaching_assignment()
    {
        var context = TimetableReviewPhase7Tests.Context() with { Assignments = [] };

        var action = () => _engine.Generate(context, default);

        action.Should().Throw<ArgumentException>().WithMessage("*أكمل إسناد مادة*");
    }

    [Fact]
    public void Generator_honors_cancellation_before_searching()
    {
        var cancellation = new CancellationToken(canceled: true);

        var action = () => _engine.Generate(TimetableReviewPhase7Tests.Context(), cancellation);

        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Generator_handles_a_realistic_eighteen_class_load_with_parallel_activity_slots()
    {
        var seed = TimetableReviewPhase7Tests.Context();
        seed.Timetable.Entries.Clear();
        var classrooms = Enumerable.Range(1, 18).Select(id => new AlFalah.Domain.Entities.StudentAffairs.Classroom
        {
            Id = id, SchoolId = 1, AcademicYearId = 1, ClassLabel = $"Class {id}"
        }).ToArray();
        var subjects = Enumerable.Range(1, 6).Select(id => new SubjectDefinition
        {
            Id = id, SchoolId = 1, Name = id == 1 ? "Activity" : $"Subject {id}"
        }).ToArray();
        var teachers = Enumerable.Range(1, 18).Select(id => new TeacherTimetableProfile
        {
            Id = id,
            SchoolId = 1,
            TimetableSetupProfileId = 1,
            InstructorProfileId = id,
            BellScheduleRevisionId = 1,
            MaximumWeeklyPeriods = 30,
            Instructor = new()
            {
                Id = id, SchoolId = 1, UserId = $"teacher-{id}", IsActive = true,
                User = new() { Id = $"teacher-{id}", FirstName = "Teacher", LastName = id.ToString(), IsActive = true }
            }
        }).ToArray();
        var requirements = new List<ClassSubjectRequirement>();
        var assignments = new List<TeachingAssignment>();
        var nextId = 0;
        foreach (var classroom in classrooms)
        foreach (var subject in subjects)
        {
            var id = ++nextId;
            var requirement = new ClassSubjectRequirement
            {
                Id = id, SchoolId = 1, TimetableSetupProfileId = 1,
                ClassroomId = classroom.Id, SubjectId = subject.Id,
                Classroom = classroom, Subject = subject, IndividualPeriodCount = 4
            };
            if (subject.Id == 1) requirement.FixedSlots.Add(new() { Day = 3, Period = 4 });
            requirements.Add(requirement);
            assignments.Add(new()
            {
                Id = id, SchoolId = 1, TimetableSetupProfileId = 1, ClassSubjectRequirementId = id,
                Members = [new()
                {
                    Id = id, SchoolId = 1, TimetableSetupProfileId = 1, TeachingAssignmentId = id,
                    TeacherTimetableProfileId = classroom.Id, AllocatedPeriodCount = 4
                }]
            });
        }
        var context = seed with
        {
            Teachers = teachers,
            Requirements = requirements,
            Assignments = assignments,
            Classrooms = classrooms,
            Subjects = subjects
        };

        var generated = _engine.Generate(context, default);

        generated.Should().HaveCount(18 * 6 * 4);
        generated.Where(x => x.SubjectId == 1 && x.Day == TimetableDay.Monday && x.Period == 4)
            .Should().HaveCount(18);
        generated.GroupBy(x => new { x.ClassroomId, x.Day, x.Period }).Should().OnlyContain(x => x.Count() == 1);
        generated.GroupBy(x => new { x.InstructorProfileId, x.Day, x.Period }).Should().OnlyContain(x => x.Count() == 1);
    }

    private static TimetableValidationContext AddRequirement(TimetableValidationContext context, int id,
        int classroomIndex, int teacherProfileId, int? fixedDay = null, int? fixedPeriod = null)
    {
        var requirement = new ClassSubjectRequirement
        {
            Id = id,
            SchoolId = 1,
            TimetableSetupProfileId = 1,
            ClassroomId = context.Classrooms[classroomIndex].Id,
            SubjectId = context.Subjects[0].Id,
            Classroom = context.Classrooms[classroomIndex],
            Subject = context.Subjects[0],
            IndividualPeriodCount = 1
        };
        if (fixedDay.HasValue && fixedPeriod.HasValue)
            requirement.FixedSlots.Add(new() { Day = fixedDay.Value, Period = fixedPeriod.Value });
        var assignment = new TeachingAssignment
        {
            Id = id,
            SchoolId = 1,
            TimetableSetupProfileId = 1,
            ClassSubjectRequirementId = id,
            Members = [new()
            {
                Id = id, SchoolId = 1, TimetableSetupProfileId = 1, TeachingAssignmentId = id,
                TeacherTimetableProfileId = teacherProfileId, AllocatedPeriodCount = 1
            }]
        };
        return context with
        {
            Requirements = [.. context.Requirements, requirement],
            Assignments = [.. context.Assignments, assignment]
        };
    }

    private static SubjectRoomRequirement RoomRequirement(int id, int roomId) => new()
    {
        Id = id, SchoolId = 1, ClassSubjectRequirementId = id, RoomId = roomId,
        Room = new() { Id = roomId, SchoolId = 1, Name = "Lab" }
    };
}
