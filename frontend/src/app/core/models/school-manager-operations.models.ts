export interface AttendanceCounts {
  total: number;
  present: number;
  absent: number;
  excused: number;
  recorded: number;
  unrecorded: number;
  attendanceRate: number | null;
  recordingRate: number | null;
}
export interface AttendanceTrend { date: string; present: number; recorded: number; }
export interface SchoolManagerOperations {
  generatedAt: string;
  date: string;
  timeZone: string;
  students: {
    counts: AttendanceCounts;
    classrooms: { classroomId: number; label: string; total: number; present: number; absent: number; excused: number; }[];
    trend: AttendanceTrend[];
  } | null;
  staff: {
    counts: AttendanceCounts;
    absences: { name: string; role: string; status: number; }[];
    trend: AttendanceTrend[];
  };
  summons: {
    pending: number; attended: number; underObservation: number; improved: number;
    appointments: { id: number; studentName: string; scheduledAt: string; priority: number; }[];
  } | null;
  schoolDay: {
    name: string; timeZone: string;
    slots: { label: string; kind: string; startsAt: string; endsAt: string; }[];
  } | null;
  activities: { category: string; title: string; at: string; route: string; }[];
  recentVisits: { id: number; teacher: string; evaluator: string; subject: string | null; date: string; status: number; score: number | null; }[];
  gatePasses: number | null;
  conversations: number | null;
}

export interface ManagerToolCard {
  id: string; label: string; icon: string; route: string; stat: string | number;
  statLabel: string; badge: number; badgeLabel: string;
  color: 'brand' | 'gold' | 'danger' | 'success' | 'info' | 'purple';
}
export interface ManagerOperationsCard {
  label: string; description: string; value: string | number; meta: string;
  icon: string; route: string; tone: 'green' | 'gold' | 'blue' | 'purple';
}
