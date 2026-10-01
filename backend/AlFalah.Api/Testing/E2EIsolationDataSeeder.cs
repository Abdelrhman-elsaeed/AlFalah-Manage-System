using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AlFalah.Api.Testing;

/// <summary>
/// E2E-only negative-scope fixtures. Registration and execution are guarded by
/// ASPNETCORE_ENVIRONMENT=E2E in Program.cs.
/// </summary>
public sealed class E2EIsolationDataSeeder(
    AlFalahDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    private const string PrimarySchoolName = "Al-Falah E2E Test School";
    private const string IsolationSchoolName = "Al-Falah E2E Isolation School";
    private const string InactiveSchoolName = "Al-Falah E2E Inactive School";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var password = configuration["ALFALAH_E2E_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("ALFALAH_E2E_PASSWORD is required for E2E fixture accounts.");

        var now = timeProvider.GetUtcNow();
        var primarySchool = await context.Schools.IgnoreQueryFilters()
            .SingleAsync(school => school.Name == PrimarySchoolName, cancellationToken);
        var actorUserId = primarySchool.ManagerUserId
            ?? throw new InvalidOperationException("The E2E primary school has no manager.");
        var term = await context.AcademicTerms.IgnoreQueryFilters()
            .SingleAsync(item => item.SchoolId == primarySchool.Id && item.IsActive, cancellationToken);
        var classroom = await context.Classrooms.IgnoreQueryFilters()
            .SingleAsync(item => item.SchoolId == primarySchool.Id && item.ClassLabel == "E2E-1-A", cancellationToken);

        await EnsureStudentAsync(
            primarySchool.Id,
            "E2E-STUDENT-UNLINKED",
            "1000000002",
            "Unlinked",
            "Student",
            actorUserId,
            now,
            term,
            classroom,
            2,
            cancellationToken);

        var otherWorker = await EnsureUserAsync(
            "socialworker.other.test",
            "socialworker.other.test@alfalah.test",
            "Other",
            "Social Worker",
            password,
            RoleNames.SocialWorker,
            cancellationToken);
        await EnsureSchoolRoleAsync(otherWorker.Id, primarySchool.Id, RoleNames.SocialWorker, cancellationToken);

        var isolationSchool = await context.Schools.IgnoreQueryFilters()
            .SingleOrDefaultAsync(school => school.Name == IsolationSchoolName, cancellationToken);
        if (isolationSchool is null)
        {
            isolationSchool = new School
            {
                Name = IsolationSchoolName,
                Stage = SchoolStage.Primary,
                City = "Giza",
                LocationDetails = "E2E cross-school isolation fixture",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Schools.Add(isolationSchool);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            isolationSchool.IsActive = true;
            isolationSchool.IsDeleted = false;
            isolationSchool.DeletedAt = null;
            isolationSchool.DeletedByUserId = null;
        }

        var crossOfficer = await EnsureUserAsync(
            "cross.officer.test",
            "cross.officer.test@alfalah.test",
            "Cross School",
            "Officer",
            password,
            RoleNames.StudentAffairsOfficer,
            cancellationToken);
        await EnsureSchoolRoleAsync(
            crossOfficer.Id,
            isolationSchool.Id,
            RoleNames.StudentAffairsOfficer,
            cancellationToken);

        var matrixInstructor = await EnsureUserAsync(
            "matrix.instructor.test",
            "matrix.instructor.test@alfalah.test",
            "Matrix",
            "Instructor",
            password,
            RoleNames.Instructor,
            cancellationToken);
        await EnsureExclusiveSchoolRoleAsync(
            matrixInstructor.Id,
            primarySchool.Id,
            RoleNames.Instructor,
            cancellationToken);
        await EnsureInstructorProfileAsync(matrixInstructor.Id, primarySchool, now, cancellationToken);

        var inactiveUser = await EnsureUserAsync(
            "inactive.user.test",
            "inactive.user.test@alfalah.test",
            "Inactive",
            "User",
            password,
            RoleNames.Moderator,
            cancellationToken);
        await EnsureSchoolRoleAsync(inactiveUser.Id, primarySchool.Id, RoleNames.Moderator, cancellationToken);
        inactiveUser.IsActive = false;
        EnsureIdentitySuccess(await userManager.UpdateAsync(inactiveUser), "deactivate inactive.user.test");

        var inactiveProfileGuardian = await EnsureUserAsync(
            "inactive.profile.guardian.test",
            "inactive.profile.guardian.test@alfalah.test",
            "Inactive Profile",
            "Guardian",
            password,
            RoleNames.Guardian,
            cancellationToken);
        await EnsureSchoolRoleAsync(inactiveProfileGuardian.Id, primarySchool.Id, RoleNames.Guardian, cancellationToken);
        await EnsureGuardianFixtureAsync(
            inactiveProfileGuardian.Id,
            primarySchool.Id,
            actorUserId,
            now,
            term,
            profileActive: false,
            relationshipActive: true,
            cancellationToken);

        var inactiveRelationshipGuardian = await EnsureUserAsync(
            "inactive.relationship.guardian.test",
            "inactive.relationship.guardian.test@alfalah.test",
            "Inactive Relationship",
            "Guardian",
            password,
            RoleNames.Guardian,
            cancellationToken);
        await EnsureSchoolRoleAsync(inactiveRelationshipGuardian.Id, primarySchool.Id, RoleNames.Guardian, cancellationToken);
        await EnsureGuardianFixtureAsync(
            inactiveRelationshipGuardian.Id,
            primarySchool.Id,
            actorUserId,
            now,
            term,
            profileActive: true,
            relationshipActive: false,
            cancellationToken);

        var inactiveSchool = await context.Schools.IgnoreQueryFilters()
            .SingleOrDefaultAsync(school => school.Name == InactiveSchoolName, cancellationToken);
        if (inactiveSchool is null)
        {
            inactiveSchool = new School
            {
                Name = InactiveSchoolName,
                Stage = SchoolStage.Primary,
                City = "Giza",
                LocationDetails = "E2E inactive-school authentication fixture",
                IsActive = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Schools.Add(inactiveSchool);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            inactiveSchool.Stage = SchoolStage.Primary;
            inactiveSchool.City = "Giza";
            inactiveSchool.LocationDetails = "E2E inactive-school authentication fixture";
            inactiveSchool.IsDeleted = false;
            inactiveSchool.DeletedAt = null;
            inactiveSchool.DeletedByUserId = null;
            inactiveSchool.UpdatedAt = now;
        }

        var inactiveSchoolUser = await EnsureUserAsync(
            "inactive.school.user.test",
            "inactive.school.user.test@alfalah.test",
            "Inactive School",
            "User",
            password,
            RoleNames.Moderator,
            cancellationToken);
        await EnsureSchoolRoleAsync(inactiveSchoolUser.Id, inactiveSchool.Id, RoleNames.Moderator, cancellationToken);
        inactiveSchool.ManagerUserId = inactiveSchoolUser.Id;
        inactiveSchool.IsActive = false;

        isolationSchool.ManagerUserId ??= crossOfficer.Id;
        var crossStudent = await context.Students.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                student => student.SchoolId == isolationSchool.Id && student.StudentNumber == "E2E-CROSS-STUDENT-001",
                cancellationToken);
        if (crossStudent is null)
        {
            context.Students.Add(new Student
            {
                SchoolId = isolationSchool.Id,
                StudentNumber = "E2E-CROSS-STUDENT-001",
                IdentityNumber = "2000000001",
                FirstName = "Cross School",
                LastName = "Student",
                Gender = StudentGender.Female,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = crossOfficer.Id,
                UpdatedByUserId = crossOfficer.Id
            });
        }
        else
        {
            crossStudent.IsActive = true;
            crossStudent.IsDeleted = false;
            crossStudent.DeletedAt = null;
            crossStudent.DeletedByUserId = null;
            crossStudent.UpdatedAt = now;
            crossStudent.UpdatedByUserId = crossOfficer.Id;
        }

        await context.SaveChangesAsync(cancellationToken);

        var fixtureFile = configuration["E2E:FixtureFile"];
        if (string.IsNullOrWhiteSpace(fixtureFile))
            throw new InvalidOperationException("E2E:FixtureFile is required for E2E fixture metadata.");
        var fixtureDirectory = Path.GetDirectoryName(fixtureFile);
        if (!string.IsNullOrWhiteSpace(fixtureDirectory)) Directory.CreateDirectory(fixtureDirectory);
        await File.WriteAllTextAsync(
            fixtureFile,
            JsonSerializer.Serialize(new { inactiveSchoolId = inactiveSchool.Id }),
            cancellationToken);
    }

    private async Task EnsureInstructorProfileAsync(
        string userId,
        School school,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profile = await context.InstructorProfiles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.SchoolId == school.Id, cancellationToken);
        if (profile is null)
        {
            context.InstructorProfiles.Add(new InstructorProfile
            {
                UserId = userId,
                SchoolId = school.Id,
                EmployeeNumber = "E2E-MATRIX-001",
                SubjectSpecialization = "Security Matrix",
                Stage = school.Stage,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            profile.EmployeeNumber = "E2E-MATRIX-001";
            profile.SubjectSpecialization = "Security Matrix";
            profile.Stage = school.Stage;
            profile.IsActive = true;
            profile.IsDeleted = false;
            profile.DeletedAt = null;
            profile.DeletedByUserId = null;
            profile.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureGuardianFixtureAsync(
        string userId,
        int schoolId,
        string actorUserId,
        DateTimeOffset now,
        AcademicTerm term,
        bool profileActive,
        bool relationshipActive,
        CancellationToken cancellationToken)
    {
        var student = await context.Students.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.SchoolId == schoolId
                && candidate.StudentNumber == (profileActive ? "E2E-STUDENT-UNLINKED" : "E2E-STUDENT-001"),
                cancellationToken);

        var profile = await context.GuardianProfiles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.SchoolId == schoolId
                && candidate.ApplicationUserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new GuardianProfile
            {
                SchoolId = schoolId,
                ApplicationUserId = userId,
                PreferredContactLanguage = PreferredContactLanguage.Arabic,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            context.GuardianProfiles.Add(profile);
            await context.SaveChangesAsync(cancellationToken);
        }
        profile.IsActive = profileActive;
        profile.IsDeleted = false;
        profile.DeletedAt = null;
        profile.DeletedByUserId = null;
        profile.UpdatedAt = now;
        profile.UpdatedByUserId = actorUserId;

        var link = await context.StudentGuardians.IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.SchoolId == schoolId
                && candidate.StudentId == student.Id
                && candidate.GuardianProfileId == profile.Id, cancellationToken);
        if (link is null)
        {
            link = new StudentGuardian
            {
                SchoolId = schoolId,
                StudentId = student.Id,
                GuardianProfileId = profile.Id,
                RelationshipType = GuardianRelationshipType.Father,
                IsPrimary = false,
                ReceivesNotifications = true,
                CanSubmitExcuses = true,
                CanRequestGatePass = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            context.StudentGuardians.Add(link);
        }
        link.IsPrimary = false;
        link.ValidFrom = term.StartsOn;
        link.ValidTo = relationshipActive ? null : DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        link.IsDeleted = false;
        link.DeletedAt = null;
        link.DeletedByUserId = null;
        link.UpdatedAt = now;
        link.UpdatedByUserId = actorUserId;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureStudentAsync(
        int schoolId,
        string studentNumber,
        string identityNumber,
        string firstName,
        string lastName,
        string actorUserId,
        DateTimeOffset now,
        AcademicTerm term,
        Classroom classroom,
        int rollNumber,
        CancellationToken cancellationToken)
    {
        var student = await context.Students.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                candidate => candidate.SchoolId == schoolId && candidate.StudentNumber == studentNumber,
                cancellationToken);
        if (student is null)
        {
            student = new Student
            {
                SchoolId = schoolId,
                StudentNumber = studentNumber,
                IdentityNumber = identityNumber,
                FirstName = firstName,
                LastName = lastName,
                Gender = StudentGender.Male,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            context.Students.Add(student);
            await context.SaveChangesAsync(cancellationToken);
        }

        var enrollment = await context.StudentEnrollments.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                candidate => candidate.SchoolId == schoolId
                    && candidate.StudentId == student.Id
                    && candidate.AcademicTermId == term.Id,
                cancellationToken);
        if (enrollment is null)
        {
            context.StudentEnrollments.Add(new StudentEnrollment
            {
                SchoolId = schoolId,
                StudentId = student.Id,
                ClassroomId = classroom.Id,
                AcademicTermId = term.Id,
                RollNumber = rollNumber,
                EnrolledOn = term.StartsOn,
                Status = StudentEnrollmentStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            });
        }
        else
        {
            enrollment.ClassroomId = classroom.Id;
            enrollment.RollNumber = rollNumber;
            enrollment.Status = StudentEnrollmentStatus.Active;
            enrollment.WithdrawnOn = null;
            enrollment.IsDeleted = false;
            enrollment.DeletedAt = null;
            enrollment.DeletedByUserId = null;
            enrollment.UpdatedAt = now;
            enrollment.UpdatedByUserId = actorUserId;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ApplicationUser> EnsureUserAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string password,
        string roleName,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = userManager.NormalizeName(username);
        var user = await context.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedUserName == normalizedUsername, cancellationToken);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                PreferredLanguage = "ar",
                IsActive = true
            };
            EnsureIdentitySuccess(await userManager.CreateAsync(user, password), $"create {username}");
        }
        else
        {
            user.IsActive = true;
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletedByUserId = null;
            EnsureIdentitySuccess(await userManager.UpdateAsync(user), $"update {username}");
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
            EnsureIdentitySuccess(await userManager.AddToRoleAsync(user, roleName), $"assign {roleName} to {username}");

        return user;
    }

    private async Task EnsureSchoolRoleAsync(
        string userId,
        int schoolId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(roleName)
            ?? throw new InvalidOperationException($"Required E2E role '{roleName}' does not exist.");
        var assignment = await context.UserSchoolRoles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == userId
                    && candidate.SchoolId == schoolId
                    && candidate.RoleId == role.Id,
                cancellationToken);
        if (assignment is null)
        {
            context.UserSchoolRoles.Add(new UserSchoolRole
            {
                UserId = userId,
                SchoolId = schoolId,
                RoleId = role.Id,
                IsActive = true
            });
        }
        else
        {
            assignment.IsActive = true;
            assignment.IsDeleted = false;
            assignment.DeletedAt = null;
            assignment.DeletedByUserId = null;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureExclusiveSchoolRoleAsync(
        string userId,
        int schoolId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Required E2E user '{userId}' does not exist.");
        var identityRoles = await userManager.GetRolesAsync(user);
        var obsoleteIdentityRoles = identityRoles
            .Where(existingRole => !string.Equals(existingRole, roleName, StringComparison.Ordinal))
            .ToArray();
        if (obsoleteIdentityRoles.Length > 0)
        {
            EnsureIdentitySuccess(
                await userManager.RemoveFromRolesAsync(user, obsoleteIdentityRoles),
                $"remove stale roles from {user.UserName}");
        }
        if (!await userManager.IsInRoleAsync(user, roleName))
            EnsureIdentitySuccess(await userManager.AddToRoleAsync(user, roleName), $"assign {roleName} to {user.UserName}");

        var expectedRole = await roleManager.FindByNameAsync(roleName)
            ?? throw new InvalidOperationException($"Required E2E role '{roleName}' does not exist.");
        var obsoleteAssignments = await context.UserSchoolRoles.IgnoreQueryFilters()
            .Where(candidate => candidate.UserId == userId
                && candidate.SchoolId == schoolId
                && candidate.RoleId != expectedRole.Id
                && candidate.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var assignment in obsoleteAssignments) assignment.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);
        await EnsureSchoolRoleAsync(userId, schoolId, roleName, cancellationToken);
    }

    private static void EnsureIdentitySuccess(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException(
            $"E2E identity seed operation failed ({operation}): "
            + string.Join(", ", result.Errors.Select(error => error.Description)));
    }
}
