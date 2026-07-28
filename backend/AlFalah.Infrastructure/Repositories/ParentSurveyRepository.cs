using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public class ParentSurveyRepository : IParentSurveyRepository
{
    private readonly AlFalahDbContext _context;

    public ParentSurveyRepository(AlFalahDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ParentSurvey>> ListAsync(
        int? schoolId,
        bool templates,
        CancellationToken cancellationToken)
    {
        var query = _context.ParentSurveys
            .AsNoTracking()
            .Where(x => x.IsTemplate == templates);

        if (schoolId.HasValue)
            query = query.Where(x => x.SchoolId == schoolId.Value);

        return await query
            .Include(x => x.School)
            .Include(x => x.Items)
            .Include(x => x.Submissions)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<ParentSurvey?> GetAsync(int id, bool track, CancellationToken cancellationToken)
    {
        var query = _context.ParentSurveys
            .Include(x => x.School)
            .Include(x => x.Items)
            .Include(x => x.Submissions)
            .Where(x => x.Id == id);

        if (!track)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ParentSurvey?> GetPublicAsync(string publicToken, bool track, CancellationToken cancellationToken)
    {
        var query = _context.ParentSurveys
            .Include(x => x.School)
            .Include(x => x.Items)
            .Where(x => x.PublicToken == publicToken && !x.IsTemplate);

        if (!track)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> PublicTokenExistsAsync(string publicToken, CancellationToken cancellationToken) =>
        _context.ParentSurveys.AnyAsync(x => x.PublicToken == publicToken, cancellationToken);

    public Task<bool> SchoolExistsAsync(int schoolId, CancellationToken cancellationToken) =>
        _context.Schools.AnyAsync(x => x.Id == schoolId && x.IsActive, cancellationToken);

    public async Task AddAsync(ParentSurvey survey, CancellationToken cancellationToken) =>
        await _context.ParentSurveys.AddAsync(survey, cancellationToken);

    public async Task AddSubmissionAsync(ParentSurveySubmission submission, CancellationToken cancellationToken) =>
        await _context.ParentSurveySubmissions.AddAsync(submission, cancellationToken);

    public async Task<IReadOnlyList<ParentSurveySubmission>> ListSubmissionsAsync(
        int surveyId,
        CancellationToken cancellationToken) =>
        await _context.ParentSurveySubmissions
            .AsNoTracking()
            .Where(x => x.ParentSurveyId == surveyId)
            .Include(x => x.Answers)
            .OrderByDescending(x => x.SubmittedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<ParentSurveySubmission?> GetSubmissionAsync(
        int surveyId,
        int submissionId,
        CancellationToken cancellationToken) =>
        _context.ParentSurveySubmissions
            .AsNoTracking()
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(
                x => x.ParentSurveyId == surveyId && x.Id == submissionId,
                cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
