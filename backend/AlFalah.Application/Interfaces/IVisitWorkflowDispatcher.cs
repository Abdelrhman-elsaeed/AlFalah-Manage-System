using AlFalah.Domain.Enums;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Routes cross-module visit workflow commands by the persisted experience
/// version without making V2 depend on the legacy visit service.
/// </summary>
public interface IVisitWorkflowDispatcher
{
    Task ReopenAsync(
        int visitId,
        ExperienceVersion experienceVersion,
        string reason,
        CancellationToken cancellationToken = default);
}
