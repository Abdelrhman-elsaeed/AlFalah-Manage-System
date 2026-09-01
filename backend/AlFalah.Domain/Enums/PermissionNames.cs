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

    #region Student Affairs Permissions

    // Student and guardian administration
    public const string StudentManage = "Student.Manage";
    public const string StudentView = "Student.View";
    public const string StudentCreate = "Student.Create";
    public const string StudentEdit = "Student.Edit";
    public const string StudentArchive = "Student.Archive";
    public const string StudentEnrollmentManage = "Student.EnrollmentManage";
    public const string ClassroomManage = "Classroom.Manage";
    public const string GuardianView = "Guardian.View";
    public const string GuardianManage = "Guardian.Manage";
    public const string GuardianLinkStudent = "Guardian.LinkStudent";
    public const string GuardianViewLinkedStudents = "Guardian.ViewLinkedStudents";

    // Student attendance and excuses
    public const string AttendanceViewStudents = "Attendance.ViewStudents";
    public const string AttendanceManageStudents = "Attendance.ManageStudents";
    public const string AttendanceSubmitExcuse = "Attendance.SubmitExcuse";
    public const string AttendanceReviewExcuse = "Attendance.ReviewExcuse";
    public const string AttendanceOverrideCorrection = "Attendance.OverrideCorrection";
    public const string MorningDelayView = "MorningDelay.View";
    public const string MorningDelayManageReason = "MorningDelay.ManageReason";
    public const string BiometricImport = "Biometric.Import";
    public const string NoorExport = "Noor.Export";

    // Teacher observations and recognition
    public const string SessionDelayView = "SessionDelay.View";
    public const string SessionDelayCreate = "SessionDelay.Create";
    public const string SessionDelayCorrect = "SessionDelay.Correct";
    public const string AcademicConcernView = "AcademicConcern.View";
    public const string AcademicConcernCreate = "AcademicConcern.Create";
    public const string AcademicConcernManage = "AcademicConcern.Manage";
    public const string BehaviorView = "Behavior.View";
    public const string BehaviorCreate = "Behavior.Create";
    public const string BehaviorManage = "Behavior.Manage";
    public const string RecognitionView = "Recognition.View";
    public const string RecognitionCreate = "Recognition.Create";
    public const string RecognitionManage = "Recognition.Manage";
    public const string RecognitionViewStatistics = "Recognition.ViewStatistics";
    public const string TeacherQuickActionView = "TeacherQuickAction.View";
    public const string TeacherQuickActionOverride = "TeacherQuickAction.Override";

    // Classroom-entry permits
    public const string ClassroomEntryPermitView = "ClassroomEntryPermit.View";
    public const string ClassroomEntryPermitIssue = "ClassroomEntryPermit.Issue";
    public const string ClassroomEntryPermitAcknowledge = "ClassroomEntryPermit.Acknowledge";
    public const string ClassroomEntryPermitRevoke = "ClassroomEntryPermit.Revoke";

    // Gate passes
    public const string GatePassView = "GatePass.View";
    public const string GatePassViewOwn = "GatePass.ViewOwn";
    public const string GatePassRequest = "GatePass.Request";
    public const string GatePassCancelOwn = "GatePass.CancelOwn";
    public const string GatePassApprove = "GatePass.Approve";
    public const string GatePassReject = "GatePass.Reject";
    public const string GatePassAcknowledgeTeacher = "GatePass.AcknowledgeTeacher";
    public const string GatePassAcknowledgeSecurity = "GatePass.AcknowledgeSecurity";
    public const string GatePassExecute = "GatePass.Execute";
    public const string GatePassOverride = "GatePass.Override";
    public const string GatePassViewAudit = "GatePass.ViewAudit";

    // Referrals, cases, and summons
    public const string ReferralView = "Referral.View";
    public const string ReferralCreate = "Referral.Create";
    public const string ReferralAssign = "Referral.Assign";
    public const string ReferralManage = "Referral.Manage";
    public const string ReferralViewConfidential = "Referral.ViewConfidential";
    public const string SummonView = "Summon.View";
    public const string SummonCreate = "Summon.Create";
    public const string SummonSchedule = "Summon.Schedule";
    public const string SummonMarkAttended = "Summon.MarkAttended";
    public const string SummonStartObservation = "Summon.StartObservation";
    public const string SummonMarkImproved = "Summon.MarkImproved";
    public const string SummonViewHistory = "Summon.ViewHistory";
    public const string SummonReviewAutomationImpact = "Summon.ReviewAutomationImpact";

    // Messaging and office hours
    public const string MessagingViewOwn = "Messaging.ViewOwn";
    public const string MessagingSend = "Messaging.Send";
    public const string MessagingStartGuardianTeacher = "Messaging.StartGuardianTeacher";
    public const string MessagingStartGuardianAdministration = "Messaging.StartGuardianAdministration";
    public const string MessagingCloseThread = "Messaging.CloseThread";
    public const string MessagingViewAudit = "Messaging.ViewAudit";
    public const string OfficeHoursView = "OfficeHours.View";
    public const string OfficeHoursManageOwn = "OfficeHours.ManageOwn";
    public const string OfficeHoursManageSchool = "OfficeHours.ManageSchool";

    // Settings, automation, notifications, and dashboards
    public const string StudentAffairsSettingsView = "StudentAffairsSettings.View";
    public const string StudentAffairsSettingsManage = "StudentAffairsSettings.Manage";
    public const string AutomationView = "Automation.View";
    public const string AutomationRetry = "Automation.Retry";
    public const string NotificationViewOwn = "Notification.ViewOwn";
    public const string NotificationApproveDispatch = "Notification.ApproveDispatch";
    public const string NotificationSuppressDispatch = "Notification.SuppressDispatch";
    public const string NotificationViewDelivery = "Notification.ViewDelivery";
    public const string StudentAffairsDashboardTeacher = "StudentAffairsDashboard.Teacher";
    public const string StudentAffairsDashboardOfficer = "StudentAffairsDashboard.Officer";
    public const string StudentAffairsDashboardSocialWorker = "StudentAffairsDashboard.SocialWorker";
    public const string StudentAffairsDashboardSecurity = "StudentAffairsDashboard.Security";
    public const string StudentAffairsDashboardGuardian = "StudentAffairsDashboard.Guardian";
    public const string StudentAffairsDashboardSchoolOversight = "StudentAffairsDashboard.SchoolOversight";

    #endregion
}
