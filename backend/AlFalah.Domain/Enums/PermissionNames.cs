namespace AlFalah.Domain.Enums;

/// <summary>
/// Permission name constants used across the system.
/// Actual permissions are seeded into the Permission table.
/// </summary>
public static class PermissionNames
{
    // School permissions
    public const string SchoolView = "School.View";
    public const string SchoolCreate = "School.Create";
    public const string SchoolEdit = "School.Edit";
    public const string SchoolDelete = "School.Delete";
    public const string SchoolDisable = "School.Disable";

    // User permissions
    public const string UserView = "User.View";
    public const string UserCreate = "User.Create";
    public const string UserEdit = "User.Edit";
    public const string UserDelete = "User.Delete";

    // Role permissions
    public const string RoleView = "Role.View";
    public const string RoleManage = "Role.Manage";

    // Instructor permissions
    public const string InstructorView = "Instructor.View";
    public const string InstructorCreate = "Instructor.Create";
    public const string InstructorEdit = "Instructor.Edit";
    public const string InstructorDelete = "Instructor.Delete";

    // Visit permissions
    public const string VisitView = "Visit.View";
    public const string VisitCreate = "Visit.Create";
    public const string VisitEdit = "Visit.Edit";
    public const string VisitDelete = "Visit.Delete";
    public const string VisitSubmit = "Visit.Submit";
    public const string VisitApprove = "Visit.Approve";
    public const string VisitReopen = "Visit.Reopen";

    // Report permissions
    public const string ReportView = "Report.View";
    public const string ReportDownload = "Report.Download";
    public const string ReportGenerate = "Report.Generate";
    public const string ReportExport = "Report.Export";

    // Rubric permissions
    public const string RubricView = "Rubric.View";
    public const string RubricManage = "Rubric.Manage";

    // Improvement plan permissions
    public const string PlanView = "Plan.View";
    public const string PlanCreate = "Plan.Create";
    public const string PlanEdit = "Plan.Edit";
    public const string PlanDelete = "Plan.Delete";

    // Follow-up permissions
    public const string FollowUpView = "FollowUp.View";
    public const string FollowUpCreate = "FollowUp.Create";
    public const string FollowUpEdit = "FollowUp.Edit";
    public const string FollowUpDelete = "FollowUp.Delete";

    // Complaint permissions
    public const string ComplaintView = "Complaint.View";
    public const string ComplaintCreate = "Complaint.Create";
    public const string ComplaintManage = "Complaint.Manage";
    public const string ComplaintDelete = "Complaint.Delete";

    // Dashboard permissions
    public const string DashboardMainManager = "Dashboard.MainManager";
    public const string DashboardSchoolManager = "Dashboard.SchoolManager";
    public const string DashboardModerator = "Dashboard.Moderator";
    public const string DashboardInstructor = "Dashboard.Instructor";

    // Settings permissions
    public const string SettingsView = "Settings.View";
    public const string SettingsManage = "Settings.Manage";

    // Audit log permissions
    public const string AuditLogView = "AuditLog.View";

    // Attendance
    public const string AttendanceView = "Attendance.View";
    public const string AttendanceManage = "Attendance.Manage";

    // School timetable
    public const string TimetableView = "Timetable.View";
    public const string TimetableManage = "Timetable.Manage";
    public const string TimetableDelegate = "Timetable.Delegate";

    // Parent surveys and reusable templates
    public const string ParentSurveyManage = "ParentSurvey.Manage";
}
