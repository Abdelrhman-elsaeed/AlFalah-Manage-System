using AlFalah.Application.DTOs.Visits;

namespace AlFalah.Application.Interfaces;

public interface IVisitFeedbackBankService
{
    Task<IReadOnlyList<VisitFeedbackTemplateDto>> ListAsync(CancellationToken cancellationToken);
    Task<VisitFeedbackTemplateDto> CreateAsync(SaveVisitFeedbackTemplateDto request, CancellationToken cancellationToken);
    Task<VisitFeedbackTemplateDto> UpdateAsync(int id, SaveVisitFeedbackTemplateDto request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
