using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Application.StudentAffairs.Messaging;

public sealed record MessagingDeliveryDecision(
    bool DeliverNow, OfficeHoursDisposition Disposition, DateTimeOffset? NextEligibleSendAt)
{
    public static MessagingDeliveryDecision Immediate { get; } =
        new(true, OfficeHoursDisposition.SentImmediately, null);
}

public static class MessagingDeliveryPolicy
{
    public static bool RequiresOfficeHours(ConversationThreadType threadType, bool senderIsTeacher) =>
        threadType == ConversationThreadType.GuardianTeacher && !senderIsTeacher;

    public static MessagingDeliveryDecision ForOfficeHours(DateTimeOffset now, DateTimeOffset? nextEligibleAt) =>
        nextEligibleAt.HasValue && nextEligibleAt.Value <= now
            ? MessagingDeliveryDecision.Immediate
            : new(false, OfficeHoursDisposition.QueuedUntilOfficeHours, nextEligibleAt);
}
