using AlFalah.Application.Interfaces;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Services;

public sealed class VisitFeedbackBankTests
{
    [Fact]
    public async Task Bank_is_school_scoped_editable_and_deleted_phrases_do_not_reappear()
    {
        var repository = new MemoryRepository();
        var schoolOne = new VisitFeedbackBankService(repository, new User(1));
        var initial = await schoolOne.ListAsync(CancellationToken.None);
        initial.Should().HaveCount(14);

        var item = await schoolOne.CreateAsync(new SaveVisitFeedbackTemplateDto(1, "عبارة إضافية"), CancellationToken.None);
        var updated = await schoolOne.UpdateAsync(item.Id,
            new SaveVisitFeedbackTemplateDto(2, "عبارة تحسين معدلة"), CancellationToken.None);
        updated.Kind.Should().Be(2);
        updated.Text.Should().Be("عبارة تحسين معدلة");

        var schoolTwo = new VisitFeedbackBankService(repository, new User(2));
        (await schoolTwo.ListAsync(CancellationToken.None)).Should().HaveCount(14);
        await FluentActions.Invoking(() => schoolTwo.DeleteAsync(item.Id, CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();

        await schoolOne.DeleteAsync(item.Id, CancellationToken.None);
        (await schoolOne.ListAsync(CancellationToken.None)).Should().NotContain(x => x.Id == item.Id);
        foreach (var phrase in (await schoolOne.ListAsync(CancellationToken.None)).ToArray())
            await schoolOne.DeleteAsync(phrase.Id, CancellationToken.None);
        (await schoolOne.ListAsync(CancellationToken.None)).Should().BeEmpty();
    }

    private sealed class MemoryRepository : IVisitFeedbackBankRepository
    {
        private readonly List<VisitFeedbackTemplate> items = [];
        private int nextId = 1;

        public Task<IReadOnlyList<VisitFeedbackTemplate>> ListAsync(int schoolId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VisitFeedbackTemplate>>(items.Where(x => x.SchoolId == schoolId && !x.IsDeleted).ToArray());
        public Task<bool> HasAnyIncludingDeletedAsync(int schoolId, CancellationToken cancellationToken) =>
            Task.FromResult(items.Any(x => x.SchoolId == schoolId));
        public Task<VisitFeedbackTemplate?> GetAsync(int schoolId, int id, CancellationToken cancellationToken) =>
            Task.FromResult(items.SingleOrDefault(x => x.SchoolId == schoolId && x.Id == id && !x.IsDeleted));
        public Task AddRangeAsync(IEnumerable<VisitFeedbackTemplate> values, CancellationToken cancellationToken)
        {
            foreach (var value in values) { value.Id = nextId++; items.Add(value); }
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class User(int schoolId) : ICurrentUserService
    {
        public string? UserId => "manager";
        public string? Username => "manager";
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => true;
        public bool HasPermission(string permissionName) => true;
        public IEnumerable<string> GetRoles() => [];
        public IEnumerable<string> GetPermissions() => [];
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
