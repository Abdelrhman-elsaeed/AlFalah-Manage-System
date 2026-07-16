// Phase 8 — Complaints (camelCase mirror of AlFalah.Application.DTOs.Complaints)

export interface Complaint {
  id: number;
  schoolId: number;
  schoolName: string;
  visitId: number;
  visitSubject: string | null;
  visitDate: string;
  instructorUserId: string;
  instructorFullName: string;
  moderatorUserId: string;
  moderatorFullName: string;
  subject: string;
  body: string;
  status: number;
  statusLabelAr: string;
  allowedNextStatuses: number[];
  resolutionNote: string | null;
  handledByUserId: string | null;
  handledByFullName: string | null;
  handledAt: string | null;
  visitReopenedAt: string | null;
  visitReopenReason: string | null;
  createdAt: string;
}

export interface CreateComplaintRequest {
  subject: string;
  body: string;
}

export interface UpdateComplaintStatusRequest {
  status: number;
  resolutionNote?: string | null;
}

export interface ReopenVisitFromComplaintRequest {
  reason: string;
}

/** ComplaintStatus enum mirror (backend ints). */
export const COMPLAINT_STATUSES: Array<{ value: number; labelKey: string }> = [
  { value: 1, labelKey: 'COMPLAINTS.STATUS_OPEN' },
  { value: 2, labelKey: 'COMPLAINTS.STATUS_IN_REVIEW' },
  { value: 3, labelKey: 'COMPLAINTS.STATUS_RESOLVED' },
  { value: 4, labelKey: 'COMPLAINTS.STATUS_REJECTED' },
  { value: 5, labelKey: 'COMPLAINTS.STATUS_CLOSED' }
];

export const COMPLAINT_STATUS_SEVERITY: Record<number, 'info' | 'warning' | 'success' | 'danger' | 'secondary'> = {
  1: 'info',
  2: 'warning',
  3: 'success',
  4: 'danger',
  5: 'secondary'
};
