import type { Page, Route } from '@playwright/test';

export type Role = 'PlatformOperator' | 'Administrator' | 'Principal' | 'SchoolInCharge' | 'Teacher' | 'Student' | 'Parent';
export const USER_ID = '11111111-1111-1111-1111-111111111111';
const OTHER_ID = '22222222-2222-2222-2222-222222222222';
const today = new Date().toISOString().slice(0, 10);

const classes = [
  { id: 'c1', name: 'Grade 5', section: 'A', classTeacherId: 't1' },
  { id: 'c2', name: 'Grade 6', section: 'B', classTeacherId: null },
];
const teachers = [
  { id: 't1', name: 'Anita Verma', contactEmail: 'anita@school.edu', telephone: '9876543210', gender: 'Female', address: 'Jaipur', joiningDate: '2024-06-01', isActive: true, deactivatedAtUtc: null },
  { id: 't2', name: 'Sameer Khan', contactEmail: 'sameer@school.edu', telephone: '9876500000', gender: 'Male', address: 'Jaipur', joiningDate: '2023-06-01', isActive: false, deactivatedAtUtc: '2026-01-01T00:00:00' },
];
const students = Array.from({ length: 14 }, (_, i) => ({
  id: `s${i + 1}`, srNumber: `SR-${100 + i}`, rollNumber: `${i + 1}`, name: `Student ${i + 1}`, dateOfBirth: '2015-04-12', gender: i % 2 ? 'Male' : 'Female',
  email: `student${i + 1}@school.edu`, classId: i < 8 ? 'c1' : 'c2', guardianFatherName: 'Raj', guardianMotherName: 'Priya', guardianContactEmail: 'guardian@mail.com', guardianPhone: '9000000000',
  isActive: i !== 13, deactivatedAtUtc: null,
}));
const record = (id: string, personId: string, personType: string, outcome: string, correctionStatus = 'None') =>
  ({ id, personId, personType, attendanceDate: today, outcome, recordedByUserId: USER_ID, recordedAtUtc: `${today}T04:00:00`, correctionStatus, locked: false });
const attendance = [
  record('a1', 's1', 'Student', 'Present'),
  record('a2', 's2', 'Student', 'Late'),
  record('a3', 's3', 'Student', 'Absent', 'CorrectionPending'),
  record('a4', 't1', 'Teacher', 'Present'),
];
const announcements = [
  { id: 'n1', title: 'Sports day on Friday', content: 'All classes assemble at 8am on the main ground.', effectiveDate: today, authorId: USER_ID, createdAtUtc: `${today}T04:00:00`, isWithdrawn: false, withdrawnAtUtc: null },
  { id: 'n2', title: 'Parent-teacher meeting', content: 'Meet your child’s class teacher.', effectiveDate: today, authorId: OTHER_ID, createdAtUtc: `${today}T04:00:00`, isWithdrawn: false, withdrawnAtUtc: null },
];
const rooms = [{ id: 'r1', name: 'Staff room', description: 'Weekly staff sync', createdByUserId: USER_ID }];
const trend = Array.from({ length: 7 }, (_, i) => ({ date: new Date(Date.now() - (6 - i) * 86400000).toISOString().slice(0, 10), studentAttendancePercent: 85 + i, teacherAttendancePercent: 90 }));
const plans = [
  { id: 'p1', name: 'Entry', learnerCapacity: 200, storageAllowanceGb: 5, supportLevel: 'Email', capabilities: [] },
  { id: 'p2', name: 'Mid', learnerCapacity: 1000, storageAllowanceGb: 25, supportLevel: 'Priority', capabilities: ['AdvancedAnalytics', 'ParentAccess'] },
  { id: 'p3', name: 'Bespoke', learnerCapacity: null, storageAllowanceGb: 500, supportLevel: 'Dedicated', capabilities: ['AdvancedAnalytics', 'ParentAccess', 'CustomReporting'] },
];

function dashboardFor(role: Role) {
  const base = { today, recentAnnouncements: announcements.map(({ id, title, effectiveDate }) => ({ id, title, effectiveDate })), upcomingEvents: [] };
  if (role === 'Student' || role === 'Parent') {
    return {
      ...base, variant: role, children: role === 'Parent'
        ? [{ studentId: 's1', name: 'Student 1', attendancePercent: 92.5, todayOutcome: 'Present' }, { studentId: 's2', name: 'Student 2', attendancePercent: 88 }]
        : [{ studentId: 's1', name: 'Student 1', attendancePercent: 92.5, todayOutcome: 'Late' }],
    };
  }
  const shared = {
    totalStudents: 13, totalClasses: 2, presentStudentsToday: 11, averageAttendancePercent: 88.2, attendanceTrend: trend,
    studentsPerClass: [{ classId: 'c1', name: 'Grade 5', section: 'A', students: 8 }, { classId: 'c2', name: 'Grade 6', section: 'B', students: 5 }],
    genderSplit: [{ gender: 'Female', students: 7 }, { gender: 'Male', students: 6 }],
  };
  if (role === 'Teacher') return { ...base, ...shared, variant: 'Teacher' };
  return { ...base, ...shared, variant: 'Leadership', totalTeachers: 2, presentTeachersToday: 1 };
}

const json = (route: Route, body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

/** Intercepts every API and hub call. `role` undefined = signed out (refresh → 401). */
export async function mockApi(page: Page, role?: Role, overrides: Record<string, (route: Route) => unknown> = {}) {
  await page.route('**/hubs/**', (route) => route.fulfill({ status: 404, body: '' })); // hub unavailable → "disconnected" state
  await page.route('**/api/v1/**', async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname.replace('/api/v1', '');
    const key = `${req.method()} ${path}`;
    if (overrides[key]) return overrides[key](route);
    switch (key) {
      case 'POST /auth/refresh':
        return role
          ? json(route, { accessToken: 'e2e-token', user: { id: USER_ID, username: `demo.${role.toLowerCase()}`, role, institutionId: 'inst-1' } })
          : json(route, { code: 'ERR-204', message: 'Your session has expired. Please sign in again.' }, 401);
      case 'POST /auth/logout': return route.fulfill({ status: 204 });
      case 'GET /public/plans': return json(route, plans);
      case 'GET /public/institutions': return json(route, [{ id: 'inst-1', name: 'Green Meadows School', city: 'Jaipur', region: 'Rajasthan' }]);
      case 'POST /public/enquiries': return json(route, { received: true }, 201);
      case 'GET /platform/summary': return json(route, { institutions: 24, students: 12394, teachers: 1823, classes: 310 });
      case 'GET /dashboard': return json(route, dashboardFor(role!));
      case 'GET /classes': return json(route, classes);
      case 'GET /teachers': return json(route, teachers);
      case 'GET /students': return json(route, students);
      case 'GET /subjects': return json(route, [{ id: 'sub1', name: 'Mathematics' }]);
      case 'GET /attendance': return json(route, attendance);
      case 'GET /attendance/roster':
        return json(route, students.slice(0, 8).map((s, i) => ({ studentId: s.id, name: s.name, currentOutcome: i < 3 ? attendance[i]!.outcome : null })));
      case 'GET /announcements': return json(route, announcements);
      case 'GET /rooms': return json(route, rooms);
      case 'GET /rooms/r1/messages':
        return json(route, [
          { id: 'm1', roomId: 'r1', authorId: OTHER_ID, content: 'Good morning, everyone.', sentAtUtc: `${today}T04:00:00`, isWithdrawn: false, revisedAtUtc: null },
          { id: 'm2', roomId: 'r1', authorId: USER_ID, content: 'Morning! Agenda is posted.', sentAtUtc: `${today}T04:01:00`, isWithdrawn: false, revisedAtUtc: null },
        ]);
      case 'POST /rooms/r1/meetings/token': return json(route, { acsToken: 'fake', expiresAtUtc: `${today}T23:00:00Z`, groupCallId: 'g1' });
      case 'GET /timetable':
        return json(route, [
          { id: 'tp1', classId: 'c1', subjectId: 'sub1', teacherId: 't1', room: '101', dayOfWeek: 'Monday', timeSlot: '09:00-09:45', isPublished: true, publishedAtUtc: `${today}T04:00:00` },
          { id: 'tp2', classId: 'c2', subjectId: 'sub1', teacherId: 't1', room: '102', dayOfWeek: 'Tuesday', timeSlot: '10:00-10:45', isPublished: false, publishedAtUtc: null },
        ]);
      case 'GET /reports':
        return json(route, [{ id: 'rr1', reportType: 'ClassAttendance', format: 'CSV', dateRange: { from: `${today.slice(0, 8)}01`, to: today }, scope: { classId: null }, status: 'Ready', requestedByUserId: USER_ID, requestedAtUtc: `${today}T04:00:00`, generatedAtUtc: `${today}T04:00:05`, failureReason: null }]);
      case 'GET /institution-config':
        return json(route, { academicYear: '2026-27', timeZone: 'Asia/Kolkata', language: 'en', notifications: { email: true, sms: false, inPlatform: true }, academic: { gradingScale: 'A-F', courseCategories: ['Core', 'Elective'], assessmentTypes: ['Unit test'], reportCardFormat: 'Termly' }, isDefault: false });
      default:
        return json(route, { code: 'ERR-902', message: 'We could not find what you are looking for.' }, 404);
    }
  });
}
