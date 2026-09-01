using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Data.Seeders;

/// <summary>
/// Provisions a deterministic, development-only school context for end-to-end testing.
/// Program.cs is the environment boundary; this seeder must never be invoked in production.
/// </summary>
public sealed class StudentAffairsDataSeeder
{
    private const string TestPassword = "Test@1234";
    private const string TestSchoolName = "Al-Falah E2E Test School";
    private const string TestSchoolCity = "Cairo";
    private const string TestStudentNumber = "E2E-STUDENT-001";
    private const string TestClassLabel = "E2E-1-A";

    private static readonly TestAccount[] TestAccounts =
    {
        new("admin.test", "admin.test@alfalah.test", "E2E", "School Manager", RoleNames.SchoolManager),
        new("officer.test", "officer.test@alfalah.test", "E2E", "Student Affairs Officer", RoleNames.StudentAffairsOfficer),
        new("socialworker.test", "socialworker.test@alfalah.test", "E2E", "Social Worker", RoleNames.SocialWorker),
        new("secretary.test", "secretary.test@alfalah.test", "E2E", "Secretary", RoleNames.Secretary),
        new("guard.test", "guard.test@alfalah.test", "E2E", "Security Guard", RoleNames.SecurityGuard),
        new("teacher.test", "teacher.test@alfalah.test", "E2E", "Teacher", RoleNames.Instructor),
        new("parent.test", "parent.test@alfalah.test", "E2E", "Parent", RoleNames.Guardian)
    };

    private readonly AlFalahDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StudentAffairsDataSeeder> _logger;

    public StudentAffairsDataSeeder(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        TimeProvider timeProvider,
        ILogger<StudentAffairsDataSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring development Student Affairs test data...");

        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var school = await EnsureSchoolAsync(cancellationToken).ConfigureAwait(false);
        var users = await EnsureAccountsAsync(school, cancellationToken).ConfigureAwait(false);

        var manager = users[RoleNames.SchoolManager];
        if (school.ManagerUserId != manager.Id)
        {
            school.ManagerUserId = manager.Id;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var academicYear = await EnsureAcademicYearAsync(today, cancellationToken).ConfigureAwait(false);
        var academicTerm = await EnsureAcademicTermAsync(
            school,
            academicYear,
            manager.Id,
            cancellationToken).ConfigureAwait(false);
        var classroom = await EnsureClassroomAsync(
            school,
            academicYear,
            manager.Id,
            cancellationToken).ConfigureAwait(false);
        var instructorProfile = await EnsureInstructorProfileAsync(
            users[RoleNames.Instructor],
            school,
            cancellationToken).ConfigureAwait(false);

        await EnsurePublishedTimetableAsync(
            school,
            academicYear,
            classroom,
            instructorProfile,
            manager.Id,
            cancellationToken).ConfigureAwait(false);
        await EnsureGuardianContextAsync(
            school,
            academicTerm,
            classroom,
            users[RoleNames.Guardian],
            manager.Id,
            today,
            cancellationToken).ConfigureAwait(false);
        await EnsureStudentAffairsSettingsAsync(
            school,
            manager.Id,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Development Student Affairs test data is ready for school {SchoolName} (Id: {SchoolId}).",
            school.Name,
            school.Id);
    }

    private async Task<School> EnsureSchoolAsync(CancellationToken cancellationToken)
    {
        var school = await _context.Schools
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.Name == TestSchoolName && candidate.City == TestSchoolCity,
                cancellationToken)
            .ConfigureAwait(false);

        if (school is null)
        {
            school = new School
            {
                Name = TestSchoolName,
                Stage = SchoolStage.Primary,
                City = TestSchoolCity,
                LocationDetails = "Development seed data",
                IsActive = true
            };
            _context.Schools.Add(school);
        }
        else
        {
            school.Stage = SchoolStage.Primary;
            school.LocationDetails = "Development seed data";
            school.IsActive = true;
            school.IsDeleted = false;
            school.DeletedAt = null;
            school.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return school;
    }

    private async Task<Dictionary<string, ApplicationUser>> EnsureAccountsAsync(
        School school,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ApplicationUser>(StringComparer.Ordinal);

        foreach (var account in TestAccounts)
        {
            var user = await EnsureUserAsync(account, cancellationToken).ConfigureAwait(false);
            await EnsureSchoolAssignmentAsync(user, school, account.Role, cancellationToken).ConfigureAwait(false);
            result.Add(account.Role, user);
        }

        return result;
    }

    private async Task<ApplicationUser> EnsureUserAsync(
        TestAccount account,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = _userManager.NormalizeName(account.UserName);
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.NormalizedUserName == normalizedUserName,
                cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = account.UserName,
                Email = account.Email,
                EmailConfirmed = true,
                FirstName = account.FirstName,
                LastName = account.LastName,
                PreferredLanguage = "ar",
                IsActive = true
            };

            EnsureIdentitySuccess(
                await _userManager.CreateAsync(user, TestPassword).ConfigureAwait(false),
                $"create {account.UserName}");
        }
        else
        {
            user.UserName = account.UserName;
            user.Email = account.Email;
            user.EmailConfirmed = true;
            user.FirstName = account.FirstName;
            user.LastName = account.LastName;
            user.PreferredLanguage = "ar";
            user.IsActive = true;
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletedByUserId = null;
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;

            EnsureIdentitySuccess(
                await _userManager.UpdateAsync(user).ConfigureAwait(false),
                $"update {account.UserName}");
        }

        if (!await _userManager.CheckPasswordAsync(user, TestPassword).ConfigureAwait(false))
        {
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                EnsureIdentitySuccess(
                    await _userManager.RemovePasswordAsync(user).ConfigureAwait(false),
                    $"remove old password for {account.UserName}");
            }

            EnsureIdentitySuccess(
                await _userManager.AddPasswordAsync(user, TestPassword).ConfigureAwait(false),
                $"set password for {account.UserName}");
        }

        if (!await _userManager.IsInRoleAsync(user, account.Role).ConfigureAwait(false))
        {
            EnsureIdentitySuccess(
                await _userManager.AddToRoleAsync(user, account.Role).ConfigureAwait(false),
                $"assign {account.Role} to {account.UserName}");
        }

        return user;
    }

    private async Task EnsureSchoolAssignmentAsync(
        ApplicationUser user,
        School school,
        string roleName,
        CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Required seed role '{roleName}' does not exist.");

        var assignment = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == user.Id
                    && candidate.SchoolId == school.Id
                    && candidate.RoleId == role.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (assignment is null)
        {
            _context.UserSchoolRoles.Add(new UserSchoolRole
            {
                UserId = user.Id,
                SchoolId = school.Id,
                RoleId = role.Id,
                IsActive = true,
                IsDeleted = false
            });
        }
        else
        {
            assignment.IsActive = true;
            assignment.IsDeleted = false;
            assignment.DeletedAt = null;
            assignment.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<AcademicYear> EnsureAcademicYearAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var academicYear = await _context.AcademicYears
            .OrderByDescending(candidate => candidate.IsActive)
            .FirstOrDefaultAsync(
                candidate => candidate.StartsOn <= today && candidate.EndsOn >= today,
                cancellationToken)
            .ConfigureAwait(false);

        if (academicYear is null)
        {
            academicYear = new AcademicYear
            {
                Code = $"DEV-E2E-{today.Year}",
                NameAr = $"Development E2E {today.Year}",
                StartsOn = today.AddMonths(-1),
                EndsOn = today.AddMonths(11),
                IsActive = true
            };
            _context.AcademicYears.Add(academicYear);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return academicYear;
    }

    private async Task<AcademicTerm> EnsureAcademicTermAsync(
        School school,
        AcademicYear academicYear,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var term = await _context.AcademicTerms
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.AcademicYearId == academicYear.Id
                    && candidate.Semester == TimetableSemester.First,
                cancellationToken)
            .ConfigureAwait(false);

        if (term is null)
        {
            term = new AcademicTerm
            {
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                Semester = TimetableSemester.First,
                StartsOn = academicYear.StartsOn,
                EndsOn = academicYear.EndsOn,
                IsActive = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.AcademicTerms.Add(term);
        }
        else
        {
            term.StartsOn = academicYear.StartsOn;
            term.EndsOn = academicYear.EndsOn;
            term.IsActive = true;
            term.UpdatedByUserId = actorUserId;
            term.IsDeleted = false;
            term.DeletedAt = null;
            term.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return term;
    }

    private async Task<Classroom> EnsureClassroomAsync(
        School school,
        AcademicYear academicYear,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var classroom = await _context.Classrooms
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.AcademicYearId == academicYear.Id
                    && candidate.ClassLabel == TestClassLabel,
                cancellationToken)
            .ConfigureAwait(false);

        if (classroom is null)
        {
            classroom = new Classroom
            {
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                Stage = school.Stage,
                GradeLevel = 1,
                Section = "A",
                ClassLabel = TestClassLabel,
                IsActive = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.Classrooms.Add(classroom);
        }
        else
        {
            classroom.Stage = school.Stage;
            classroom.GradeLevel = 1;
            classroom.Section = "A";
            classroom.IsActive = true;
            classroom.UpdatedByUserId = actorUserId;
            classroom.IsDeleted = false;
            classroom.DeletedAt = null;
            classroom.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return classroom;
    }

    private async Task<InstructorProfile> EnsureInstructorProfileAsync(
        ApplicationUser instructor,
        School school,
        CancellationToken cancellationToken)
    {
        var profile = await _context.InstructorProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.UserId == instructor.Id, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            profile = new InstructorProfile
            {
                UserId = instructor.Id,
                SchoolId = school.Id,
                SubjectSpecialization = "Mathematics",
                Stage = school.Stage,
                EmployeeNumber = "E2E-TEACHER-001",
                IsActive = true
            };
            _context.InstructorProfiles.Add(profile);
        }
        else
        {
            profile.SchoolId = school.Id;
            profile.SubjectSpecialization = "Mathematics";
            profile.Stage = school.Stage;
            profile.EmployeeNumber = "E2E-TEACHER-001";
            profile.IsActive = true;
            profile.IsDeleted = false;
            profile.DeletedAt = null;
            profile.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return profile;
    }

    private async Task EnsurePublishedTimetableAsync(
        School school,
        AcademicYear academicYear,
        Classroom classroom,
        InstructorProfile instructor,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var timetable = await _context.SchoolTimetables
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.AcademicYearId == academicYear.Id
                    && candidate.Semester == TimetableSemester.First,
                cancellationToken)
            .ConfigureAwait(false);

        if (timetable is null)
        {
            timetable = new SchoolTimetable
            {
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                Semester = TimetableSemester.First,
                Title = "E2E Published Timetable",
                IsPublished = true,
                PublishedAt = _timeProvider.GetUtcNow(),
                PublishedByUserId = actorUserId,
                Revision = 1,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.SchoolTimetables.Add(timetable);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            timetable.Title = "E2E Published Timetable";
            timetable.IsPublished = true;
            timetable.PublishedAt ??= _timeProvider.GetUtcNow();
            timetable.PublishedByUserId = actorUserId;
            timetable.UpdatedByUserId = actorUserId;
            timetable.IsDeleted = false;
            timetable.DeletedAt = null;
            timetable.DeletedByUserId = null;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var existingEntries = await _context.SchoolTimetableEntries
            .IgnoreQueryFilters()
            .Where(candidate => candidate.SchoolId == school.Id
                && candidate.SchoolTimetableId == timetable.Id
                && candidate.InstructorProfileId == instructor.Id)
            .ToDictionaryAsync(candidate => (candidate.Day, candidate.Period), cancellationToken)
            .ConfigureAwait(false);

        foreach (var day in Enum.GetValues<TimetableDay>())
        {
            for (byte period = 1; period <= 8; period++)
            {
                if (!existingEntries.TryGetValue((day, period), out var entry))
                {
                    _context.SchoolTimetableEntries.Add(new SchoolTimetableEntry
                    {
                        SchoolId = school.Id,
                        SchoolTimetableId = timetable.Id,
                        ClassroomId = classroom.Id,
                        InstructorProfileId = instructor.Id,
                        Day = day,
                        Period = period,
                        EntryType = TimetableEntryType.Lesson,
                        ClassLabel = classroom.ClassLabel,
                        Subject = instructor.SubjectSpecialization
                    });
                    continue;
                }

                entry.ClassroomId = classroom.Id;
                entry.EntryType = TimetableEntryType.Lesson;
                entry.ClassLabel = classroom.ClassLabel;
                entry.Subject = instructor.SubjectSpecialization;
                entry.IsDeleted = false;
                entry.DeletedAt = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureGuardianContextAsync(
        School school,
        AcademicTerm academicTerm,
        Classroom classroom,
        ApplicationUser guardianUser,
        string actorUserId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var guardian = await _context.GuardianProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.ApplicationUserId == guardianUser.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (guardian is null)
        {
            guardian = new GuardianProfile
            {
                SchoolId = school.Id,
                ApplicationUserId = guardianUser.Id,
                PreferredContactLanguage = PreferredContactLanguage.Arabic,
                IsActive = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.GuardianProfiles.Add(guardian);
        }
        else
        {
            guardian.PreferredContactLanguage = PreferredContactLanguage.Arabic;
            guardian.IsActive = true;
            guardian.UpdatedByUserId = actorUserId;
            guardian.IsDeleted = false;
            guardian.DeletedAt = null;
            guardian.DeletedByUserId = null;
        }

        var student = await _context.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.StudentNumber == TestStudentNumber,
                cancellationToken)
            .ConfigureAwait(false);

        if (student is null)
        {
            student = new Student
            {
                SchoolId = school.Id,
                StudentNumber = TestStudentNumber,
                FirstName = "E2E",
                LastName = "Student",
                Gender = StudentGender.Male,
                IsActive = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.Students.Add(student);
        }
        else
        {
            student.FirstName = "E2E";
            student.LastName = "Student";
            student.IsActive = true;
            student.UpdatedByUserId = actorUserId;
            student.IsDeleted = false;
            student.DeletedAt = null;
            student.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var link = await _context.StudentGuardians
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.StudentId == student.Id
                    && candidate.GuardianProfileId == guardian.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (link is null)
        {
            link = new StudentGuardian
            {
                SchoolId = school.Id,
                StudentId = student.Id,
                GuardianProfileId = guardian.Id,
                RelationshipType = GuardianRelationshipType.Father,
                IsPrimary = true,
                ReceivesNotifications = true,
                CanSubmitExcuses = true,
                CanRequestGatePass = true,
                ValidFrom = academicTerm.StartsOn,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.StudentGuardians.Add(link);
        }
        else
        {
            link.RelationshipType = GuardianRelationshipType.Father;
            link.IsPrimary = true;
            link.ReceivesNotifications = true;
            link.CanSubmitExcuses = true;
            link.CanRequestGatePass = true;
            link.ValidFrom = academicTerm.StartsOn;
            link.ValidTo = null;
            link.UpdatedByUserId = actorUserId;
            link.IsDeleted = false;
            link.DeletedAt = null;
            link.DeletedByUserId = null;
        }

        var enrollment = await _context.StudentEnrollments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.StudentId == student.Id
                    && candidate.AcademicTermId == academicTerm.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (enrollment is null)
        {
            enrollment = new StudentEnrollment
            {
                SchoolId = school.Id,
                StudentId = student.Id,
                ClassroomId = classroom.Id,
                AcademicTermId = academicTerm.Id,
                RollNumber = 1,
                EnrolledOn = today < academicTerm.StartsOn ? academicTerm.StartsOn : today,
                Status = StudentEnrollmentStatus.Active,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.StudentEnrollments.Add(enrollment);
        }
        else
        {
            enrollment.ClassroomId = classroom.Id;
            enrollment.RollNumber = 1;
            enrollment.WithdrawnOn = null;
            enrollment.Status = StudentEnrollmentStatus.Active;
            enrollment.UpdatedByUserId = actorUserId;
            enrollment.IsDeleted = false;
            enrollment.DeletedAt = null;
            enrollment.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureStudentAffairsSettingsAsync(
        School school,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var settings = await _context.SchoolStudentAffairsSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.SchoolId == school.Id, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            _context.SchoolStudentAffairsSettings.Add(new SchoolStudentAffairsSettings
            {
                SchoolId = school.Id,
                MorningDelayThresholdPerTerm = 10,
                BehaviorIncidentMultiplePerTerm = 10,
                AcademicConcernThresholdPerTerm = 3,
                ClassroomEntryPermitThresholdPerTerm = 5,
                AbsenceVisualAlertThresholdPerTerm = 3,
                AbsenceReferralThresholdPerTerm = 5,
                AbsenceChildRightsThresholdPerTerm = 10,
                BehaviorCountabilityPolicy = "ApprovedOnly",
                ArrivalCutoffLocalTime = new TimeOnly(7, 0),
                ArrivalGraceMinutes = 10,
                Version = 1,
                EffectiveFrom = _timeProvider.GetUtcNow(),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            });
        }
        else
        {
            settings.UpdatedByUserId = actorUserId;
            settings.IsDeleted = false;
            settings.DeletedAt = null;
            settings.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureIdentitySuccess(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Identity seed operation failed ({operation}): "
            + string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    private sealed record TestAccount(
        string UserName,
        string Email,
        string FirstName,
        string LastName,
        string Role);
}
