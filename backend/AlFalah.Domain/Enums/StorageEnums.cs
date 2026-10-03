namespace AlFalah.Domain.Enums;

public enum StorageFolderKind { Teacher = 1, SchoolLibrary = 2, VisitArchive = 3 }
public enum StoredFileSourceKind { TeacherUpload = 1, SchoolUpload = 2, VisitArchive = 3, HistoricalImport = 4 }
public enum StoredFileAvailability { Unverified = 1, Available = 2, Missing = 3, Deleted = 4, UploadIncomplete = 5 }
public enum EvidenceLinkStatus { Draft = 1, PendingReview = 2, Approved = 3, Rejected = 4, Resubmitted = 5 }
public enum EvidenceFulfillmentPolicy { AnyApprovedLink = 1, MinimumApprovedLinks = 2 }
public enum EvidenceImportance { Normal = 1, Important = 2, Critical = 3 }
public enum VisitArchiveStatus { Pending = 1, Processing = 2, Completed = 3, RetryScheduled = 4, NeedsAttention = 5 }
public enum PrototypeImportStatus { Preview = 1, ReferenceOnly = 2, Committed = 3, Exception = 4 }
