using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;

namespace AlFalah.Infrastructure.Services;

public sealed class VisitWorkflowDispatcher : IVisitWorkflowDispatcher
{
    private readonly IVisitV2Service _visitsV2;

    public VisitWorkflowDispatcher(IVisitV2Service visitsV2)
    {
        _visitsV2 = visitsV2;
    }

    public async Task ReopenAsync(
        int visitId,
        ExperienceVersion experienceVersion,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (experienceVersion == ExperienceVersion.PrototypeV2)
        {
            await _visitsV2.ReopenAsync(visitId, reason, cancellationToken);
            return;
        }

        throw new BusinessRuleException(
            "سجل الزيارات القديم متاح للقراءة فقط ولا يمكن إعادة فتحه بعد الانتقال إلى V2.");
    }
}
