using AlFalah.Application.DTOs.ParentSurveys;

namespace AlFalah.Application.Interfaces;

public interface IParentSurveyService
{
    Task<IReadOnlyList<ParentSurveyDto>> ListAsync(bool templates, int? schoolId, CancellationToken cancellationToken = default);
    Task<ParentSurveyDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<ParentSurveyDto> CreateAsync(SaveParentSurveyRequestDto request, CancellationToken cancellationToken = default);
    Task<ParentSurveyDto> UpdateAsync(int id, SaveParentSurveyRequestDto request, CancellationToken cancellationToken = default);
    Task<PublishParentSurveyDto> PublishAsync(int id, CancellationToken cancellationToken = default);
    Task CloseAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<PublicParentSurveyDto> GetPublicAsync(string publicToken, CancellationToken cancellationToken = default);
    Task SubmitAsync(string publicToken, SubmitParentSurveyRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParentSurveySubmissionListItemDto>> ListSubmissionsAsync(int surveyId, CancellationToken cancellationToken = default);
    Task<ParentSurveySubmissionDto> GetSubmissionAsync(int surveyId, int submissionId, CancellationToken cancellationToken = default);
}
