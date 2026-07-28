using AlFalah.Application.Common;
using AlFalah.Application.DTOs.ParentSurveys;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.ParentSurveys;

public class ParentSurveyServiceTests
{
    [Fact]
    public async Task SubmitAsync_WeakWithoutReason_BecomesVeryGoodButWeakWithReasonIsPreserved()
    {
        await using var context = CreateContext();
        var school = new School
        {
            Id = 1,
            Name = "مدارس الفلاح",
            City = "القاهرة",
            Stage = SchoolStage.Primary
        };
        var survey = new ParentSurvey
        {
            Id = 10,
            SchoolId = school.Id,
            School = school,
            Title = "تقييم الخدمات",
            PublicToken = "public-token",
            Status = ParentSurveyStatus.Published,
            CreatedByUserId = "manager",
            Items =
            {
                new ParentSurveyItem { Id = 101, Text = "النظافة", SortOrder = 1 },
                new ParentSurveyItem { Id = 102, Text = "التواصل", SortOrder = 2 }
            }
        };
        context.Schools.Add(school);
        context.ParentSurveys.Add(survey);
        await context.SaveChangesAsync();

        var repository = new ParentSurveyRepository(context);
        var currentUser = new SchoolScopedCurrentUserService(RoleNames.Moderator, school.Id);
        var scopeGuard = new SchoolScopeGuard(context, currentUser, NullLogger<SchoolScopeGuard>.Instance);
        var audit = new AuditLogWriter(
            context,
            new HttpContextAccessor(),
            NullLogger<AuditLogWriter>.Instance);
        var service = new ParentSurveyService(repository, currentUser, scopeGuard, audit);

        await service.SubmitAsync(
            "public-token",
            new SubmitParentSurveyRequestDto(
                "ولي أمر الطالب",
                "+201001234567",
                new[]
                {
                    new SubmitParentSurveyAnswerDto(101, ParentSurveyRating.Weak, "  "),
                    new SubmitParentSurveyAnswerDto(102, ParentSurveyRating.Weak, "تأخر الرد")
                }));

        var answers = await context.ParentSurveyAnswers
            .OrderBy(x => x.ParentSurveyItemId)
            .ToListAsync();

        answers.Should().HaveCount(2);
        answers[0].SubmittedRating.Should().Be(ParentSurveyRating.Weak);
        answers[0].EffectiveRating.Should().Be(ParentSurveyRating.VeryGood);
        answers[0].WasAutoAdjusted.Should().BeTrue();
        answers[0].WeakReason.Should().BeNull();

        answers[1].SubmittedRating.Should().Be(ParentSurveyRating.Weak);
        answers[1].EffectiveRating.Should().Be(ParentSurveyRating.Weak);
        answers[1].WasAutoAdjusted.Should().BeFalse();
        answers[1].WeakReason.Should().Be("تأخر الرد");

        var moderatorRows = await service.ListSubmissionsAsync(survey.Id);
        moderatorRows.Should().ContainSingle();
        moderatorRows[0].ParentName.Should().Be("ولي أمر الطالب");

        var moderatorDetail = await service.GetSubmissionAsync(survey.Id, moderatorRows[0].Id);
        moderatorDetail.Answers.Should().HaveCount(2);
        moderatorDetail.MobileNumber.Should().Be("+201001234567");
    }

    [Theory]
    [InlineData(RoleNames.Moderator)]
    [InlineData(RoleNames.SchoolManager)]
    public async Task SchoolScopedUser_CanOnlyAccessOwnSchoolsFormsTemplatesAndReplies(string role)
    {
        await using var context = CreateContext();
        var firstSchool = new School
        {
            Id = 1,
            Name = "First School",
            City = "Cairo",
            Stage = SchoolStage.Primary
        };
        var secondSchool = new School
        {
            Id = 2,
            Name = "Second School",
            City = "Giza",
            Stage = SchoolStage.Primary
        };

        var ownForm = CreateSurvey(10, firstSchool, "Own form", isTemplate: false);
        var ownTemplate = CreateSurvey(11, firstSchool, "Own template", isTemplate: true);
        var otherForm = CreateSurvey(20, secondSchool, "Other form", isTemplate: false);
        var otherTemplate = CreateSurvey(21, secondSchool, "Other template", isTemplate: true);
        otherForm.Submissions.Add(new ParentSurveySubmission
        {
            Id = 201,
            ParentName = "Other parent",
            MobileNumber = "+201000000000"
        });

        context.Schools.AddRange(firstSchool, secondSchool);
        context.ParentSurveys.AddRange(ownForm, ownTemplate, otherForm, otherTemplate);
        await context.SaveChangesAsync();

        var service = CreateService(context, new SchoolScopedCurrentUserService(role, firstSchool.Id));

        var forms = await service.ListAsync(templates: false, schoolId: null);
        forms.Should().ContainSingle(x => x.Id == ownForm.Id);
        forms.Should().OnlyContain(x => x.SchoolId == firstSchool.Id);

        var tamperedForms = await service.ListAsync(templates: false, schoolId: secondSchool.Id);
        tamperedForms.Should().ContainSingle(x => x.Id == ownForm.Id);
        tamperedForms.Should().OnlyContain(x => x.SchoolId == firstSchool.Id);

        var templates = await service.ListAsync(templates: true, schoolId: null);
        templates.Should().ContainSingle(x => x.Id == ownTemplate.Id);
        templates.Should().OnlyContain(x => x.SchoolId == firstSchool.Id);

        var openOtherForm = async () => await service.GetAsync(otherForm.Id);
        var openOtherTemplate = async () => await service.GetAsync(otherTemplate.Id);
        var listOtherReplies = async () => await service.ListSubmissionsAsync(otherForm.Id);
        var openOtherReply = async () => await service.GetSubmissionAsync(otherForm.Id, 201);

        await openOtherForm.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await openOtherTemplate.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await listOtherReplies.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await openOtherReply.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Theory]
    [InlineData(RoleNames.Moderator)]
    [InlineData(RoleNames.SchoolManager)]
    public async Task SchoolScopedUser_CannotCreateFormFromAnotherSchoolsTemplate(string role)
    {
        await using var context = CreateContext();
        var firstSchool = new School
        {
            Id = 1,
            Name = "First School",
            City = "Cairo",
            Stage = SchoolStage.Primary
        };
        var secondSchool = new School
        {
            Id = 2,
            Name = "Second School",
            City = "Giza",
            Stage = SchoolStage.Primary
        };
        var otherTemplate = CreateSurvey(21, secondSchool, "Other template", isTemplate: true);

        context.Schools.AddRange(firstSchool, secondSchool);
        context.ParentSurveys.Add(otherTemplate);
        await context.SaveChangesAsync();

        var service = CreateService(context, new SchoolScopedCurrentUserService(role, firstSchool.Id));
        var request = new SaveParentSurveyRequestDto(
            firstSchool.Id,
            "New form",
            null,
            IsTemplate: false,
            SourceTemplateId: otherTemplate.Id,
            Items: Array.Empty<ParentSurveyItemWriteDto>());

        var createFromOtherTemplate = async () => await service.CreateAsync(request);

        await createFromOtherTemplate.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        (await context.ParentSurveys.CountAsync()).Should().Be(1);
    }

    private static ParentSurveyService CreateService(
        AlFalahDbContext context,
        ICurrentUserService currentUser)
    {
        var repository = new ParentSurveyRepository(context);
        var scopeGuard = new SchoolScopeGuard(
            context,
            currentUser,
            NullLogger<SchoolScopeGuard>.Instance);
        var audit = new AuditLogWriter(
            context,
            new HttpContextAccessor(),
            NullLogger<AuditLogWriter>.Instance);
        return new ParentSurveyService(repository, currentUser, scopeGuard, audit);
    }

    private static ParentSurvey CreateSurvey(
        int id,
        School school,
        string title,
        bool isTemplate)
    {
        return new ParentSurvey
        {
            Id = id,
            SchoolId = school.Id,
            School = school,
            Title = title,
            IsTemplate = isTemplate,
            Status = ParentSurveyStatus.Draft,
            CreatedByUserId = "creator",
            Items =
            {
                new ParentSurveyItem
                {
                    Id = id * 10,
                    Text = "Evaluation item",
                    SortOrder = 1
                }
            }
        };
    }

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AlFalahDbContext(options);
    }

    private sealed class SchoolScopedCurrentUserService : ICurrentUserService
    {
        private readonly string _role;

        public SchoolScopedCurrentUserService(string role, int activeSchoolId)
        {
            _role = role;
            ActiveSchoolId = activeSchoolId;
        }

        public string? UserId => "school-user";
        public string? Username => "school-user";
        public int? ActiveSchoolId { get; }
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == _role;
        public bool HasPermission(string permissionName) => permissionName == PermissionNames.ParentSurveyManage;
        public IEnumerable<string> GetRoles() => new[] { _role };
        public IEnumerable<string> GetPermissions() => new[] { PermissionNames.ParentSurveyManage };
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
