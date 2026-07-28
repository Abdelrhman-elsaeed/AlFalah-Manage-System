using AlFalah.Domain.Entities;

namespace AlFalah.Application.Interfaces;

public interface IParentSurveyRepository
{
    Task<IReadOnlyList<ParentSurvey>> ListAsync(int? schoolId, bool templates, CancellationToken cancellationToken);
    Task<ParentSurvey?> GetAsync(int id, bool track, CancellationToken cancellationToken);
    Task<ParentSurvey?> GetPublicAsync(string publicToken, bool track, CancellationToken cancellationToken);
    Task<bool> PublicTokenExistsAsync(string publicToken, CancellationToken cancellationToken);
    Task<bool> SchoolExistsAsync(int schoolId, CancellationToken cancellationToken);
    Task AddAsync(ParentSurvey survey, CancellationToken cancellationToken);
    Task AddSubmissionAsync(ParentSurveySubmission submission, CancellationToken cancellationToken);
    Task<IReadOnlyList<ParentSurveySubmission>> ListSubmissionsAsync(int surveyId, CancellationToken cancellationToken);
    Task<ParentSurveySubmission?> GetSubmissionAsync(int surveyId, int submissionId, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
