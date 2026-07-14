namespace AlFalah.Application.DTOs.Complaints;

/// <summary>Phase 8 — complaint read DTO. Status is the int enum value; the
/// Arabic label + the allowed next statuses are computed server-side so the
/// frontend never re-implements the state machine.</summary>
public class ComplaintDto
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;

    public int VisitId { get; set; }
    public string? VisitSubject { get; set; }
    public DateTimeOffset VisitDate { get; set; }

    public string InstructorUserId { get; set; } = string.Empty;
    public string InstructorFullName { get; set; } = string.Empty;
    public string ModeratorUserId { get; set; } = string.Empty;
    public string ModeratorFullName { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public int Status { get; set; }
    public string StatusLabelAr { get; set; } = string.Empty;
    public List<int> AllowedNextStatuses { get; set; } = new();

    public string? ResolutionNote { get; set; }
    public string? HandledByUserId { get; set; }
    public string? HandledByFullName { get; set; }
    public DateTimeOffset? HandledAt { get; set; }

    public DateTimeOffset? VisitReopenedAt { get; set; }
    public string? VisitReopenReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Instructor submission payload.</summary>
public class CreateComplaintRequestDto
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>Handler status-change payload (SM / SuperAdmin).</summary>
public class UpdateComplaintStatusRequestDto
{
    /// <summary>Target ComplaintStatus int value (1..5).</summary>
    public int Status { get; set; }
    public string? ResolutionNote { get; set; }
}

/// <summary>Reopen-visit-from-complaint payload (SM / SuperAdmin).</summary>
public class ReopenVisitFromComplaintRequestDto
{
    public string Reason { get; set; } = string.Empty;
}
