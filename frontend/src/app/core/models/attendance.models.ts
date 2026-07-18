export type AttendanceStatus = 1 | 2 | 3;

export interface AttendanceSheetRow {
  userId: string;
  fullName: string;
  role: string;
  status: AttendanceStatus | null;
  notes: string | null;
  recordedAt: string | null;
}

export interface AttendanceSheet {
  date: string;
  rows: AttendanceSheetRow[];
}

export interface SaveAttendanceSheetRequest {
  date: string;
  entries: Array<{ userId: string; status: AttendanceStatus; notes?: string | null }>;
  schoolId?: number;
}

export interface MyAttendanceItem {
  date: string;
  status: AttendanceStatus;
  notes: string | null;
  recordedAt: string;
}

export interface AttendanceRecordItem extends MyAttendanceItem {
  userId: string;
  fullName: string;
  role: string;
}
