// Wire types for /api/v1 — mirror the controllers exactly (camelCase JSON, enums as PascalCase strings,
// DateOnly as "yyyy-MM-dd"). Only the shapes the SPA consumes are declared.
import type { Role } from './roles';

export interface ApiErrorBody { code: string; message: string }

export interface SessionUser { id: string; username: string; role: Role; institutionId: string }
export interface SessionResponse { accessToken: string; user: SessionUser }

export interface DirectoryEntry { id: string; name: string; city: string; region: string }

export type PlanCapability = 'AdvancedAnalytics' | 'ParentAccess' | 'CustomReporting';
export interface Plan {
  id: string;
  name: string;
  learnerCapacity: number | null;
  storageAllowanceGb: number;
  supportLevel: string;
  capabilities: PlanCapability[];
}

export interface SchoolClass { id: string; name: string; section: string; classTeacherId: string | null }
export interface Subject { id: string; name: string }
export interface Teacher {
  id: string; name: string; contactEmail: string; telephone: string; gender: string; address: string;
  joiningDate: string; isActive: boolean; deactivatedAtUtc: string | null;
}
export interface Student {
  id: string; srNumber: string; rollNumber: string; name: string; dateOfBirth: string; gender: string; email: string;
  classId: string | null;
  guardianFatherName: string | null; guardianMotherName: string | null;
  guardianContactEmail: string | null; guardianPhone: string | null;
  isActive: boolean; deactivatedAtUtc: string | null;
}
export interface ParentLinkCodeIssued { codeId: string; code: string; expiresAtUtc: string }

export type AttendanceOutcome = 'Present' | 'Absent' | 'Late' | 'Leave';
export interface RosterRow { studentId: string; name: string; currentOutcome: AttendanceOutcome | null }
export interface AttendanceRecord {
  id: string; personId: string; personType: 'Student' | 'Teacher'; attendanceDate: string; outcome: AttendanceOutcome;
  recordedByUserId: string; recordedAtUtc: string; correctionStatus: 'None' | 'CorrectionPending'; locked: boolean;
}
export interface CorrectionRequest {
  id: string; attendanceRecordId: string; requestedOutcome: AttendanceOutcome;
  status: 'Pending' | 'Approved' | 'Rejected' | 'AutoRejected' | 'AppliedDirectly' | 'Superseded';
  recordOutcome: AttendanceOutcome; reason: string; resolutionNote: string | null;
}

export interface Announcement {
  id: string; title: string; content: string; effectiveDate: string; authorId: string; createdAtUtc: string;
  isWithdrawn: boolean; withdrawnAtUtc: string | null;
}
export interface Room { id: string; name: string; description: string | null; createdByUserId: string }
export interface Message {
  id: string; roomId: string; authorId: string; content: string; sentAtUtc: string; isWithdrawn: boolean; revisedAtUtc: string | null;
}
export interface MeetingToken { acsToken: string; expiresAtUtc: string; groupCallId: string }

export interface TrendPoint { date: string; studentAttendancePercent?: number; teacherAttendancePercent?: number }
export interface ClassCount { classId: string; name: string; section: string; students: number }
export interface GenderCount { gender: string; students: number }
export interface AnnouncementSummary { id: string; title: string; effectiveDate: string }
export interface ChildAttendance { studentId: string; name: string; attendancePercent?: number; todayOutcome?: AttendanceOutcome }
export interface Dashboard {
  variant: 'Leadership' | 'Teacher' | 'Student' | 'Parent';
  today: string;
  recentAnnouncements: AnnouncementSummary[];
  upcomingEvents: AnnouncementSummary[];
  totalStudents?: number; totalTeachers?: number; totalClasses?: number;
  presentStudentsToday?: number; presentTeachersToday?: number;
  averageAttendancePercent?: number;
  attendanceTrend?: TrendPoint[];
  studentsPerClass?: ClassCount[];
  genderSplit?: GenderCount[];
  children?: ChildAttendance[];
}
export interface PlatformSummary { institutions: number; students: number; teachers: number; classes: number }

export type DayOfWeekSchool = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';
export interface TimetablePeriod {
  id: string; classId: string; subjectId: string; teacherId: string; room: string; dayOfWeek: DayOfWeekSchool; timeSlot: string;
  isPublished: boolean; publishedAtUtc: string | null;
}

export type ReportType = 'ClassAttendance' | 'StudentPerformance' | 'CourseAnalytics' | 'TeacherPerformance';
export interface ReportRun {
  id: string; reportType: ReportType; format: 'CSV'; dateRange: { from: string; to: string }; scope: { classId: string | null };
  status: 'Pending' | 'Ready' | 'Failed'; requestedByUserId: string; requestedAtUtc: string;
  generatedAtUtc: string | null; failureReason: string | null;
}

export interface InstitutionConfig {
  academicYear: string; timeZone: string; language: string;
  notifications: { email: boolean; sms: boolean; inPlatform: boolean };
  academic: { gradingScale: string; courseCategories: string[]; assessmentTypes: string[]; reportCardFormat: string };
  isDefault: boolean;
}
